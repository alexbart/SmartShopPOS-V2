using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartShopPOS.Application.Catalog;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Contracts.Catalog;
using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Catalog;

public sealed class ProductCatalogService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : IProductCatalogService
{
    public async Task<CatalogResult<IReadOnlyList<ProductResponse>>> ListProductsAsync(
        ProductFilter filter,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("products.view", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<IReadOnlyList<ProductResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var query = dbContext.Products.Where(product => product.OrganizationId == organizationId);
        if (filter.Active is bool active)
        {
            query = query.Where(product => product.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(filter.Sku))
        {
            var sku = filter.Sku.Trim().ToUpperInvariant();
            query = query.Where(product => product.Sku == sku);
        }

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var barcode = filter.Barcode.Trim().ToUpperInvariant();
            query = query.Where(product => product.Barcode == barcode);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(product => product.Name.ToLower().Contains(search) ||
                product.Sku.ToLower().Contains(search) ||
                product.Barcode != null && product.Barcode.ToLower().Contains(search));
        }

        if (filter.CategoryId is Guid categoryId)
        {
            query = query.Where(product => product.CategoryId == categoryId);
        }

        if (filter.BrandId is Guid brandId)
        {
            query = query.Where(product => product.BrandId == brandId);
        }

        var products = await query.OrderBy(product => product.Sku).ToListAsync(cancellationToken);
        return CatalogResult<IReadOnlyList<ProductResponse>>.Success(products.Select(ToResponse).ToList());
    }

    public async Task<CatalogResult<ProductResponse>> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("products.view", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<ProductResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId && candidate.OrganizationId == currentUser.OrganizationId,
            cancellationToken);
        return product is null
            ? CatalogResult<ProductResponse>.Failure(CatalogError.NotFound, "Product not found.")
            : CatalogResult<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<CatalogResult<ProductResponse>> CreateProductAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("products.create", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<ProductResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Invalid, "A product request is required.");
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var references = await ValidateProductReferencesAsync(organizationId, request, cancellationToken);
        if (references is not null)
        {
            return CatalogResult<ProductResponse>.Failure(references.Value, ReferenceMessage(references.Value));
        }

        Product product;
        try
        {
            product = new Product(
                organizationId,
                request.Sku,
                request.Barcode,
                request.Name,
                request.Description,
                request.CategoryId,
                request.BrandId,
                request.UnitOfMeasureId,
                request.TaxCategoryId);
        }
        catch (DomainException exception)
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await ProductIdentityExistsAsync(organizationId, product.Sku, product.Barcode, null, cancellationToken))
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Conflict, "The SKU or barcode is already used by a product in this organization.");
        }

        dbContext.Products.Add(product);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Conflict, "The SKU or barcode is already used by a product in this organization.");
        }

        return CatalogResult<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<CatalogResult<ProductResponse>> UpdateProductAsync(
        Guid productId,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("products.update", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<ProductResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Invalid, "A product request is required.");
        }

        var organizationId = currentUser.OrganizationId!.Value;
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId && candidate.OrganizationId == organizationId,
            cancellationToken);
        if (product is null)
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.NotFound, "Product not found.");
        }

        var references = await ValidateProductReferencesAsync(organizationId, request, cancellationToken);
        if (references is not null)
        {
            return CatalogResult<ProductResponse>.Failure(references.Value, ReferenceMessage(references.Value));
        }

        try
        {
            product.Update(
                request.Sku,
                request.Barcode,
                request.Name,
                request.Description,
                request.CategoryId,
                request.BrandId,
                request.UnitOfMeasureId,
                request.TaxCategoryId);
        }
        catch (DomainException exception)
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await ProductIdentityExistsAsync(organizationId, product.Sku, product.Barcode, product.Id, cancellationToken))
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Conflict, "The SKU or barcode is already used by another product in this organization.");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<ProductResponse>.Failure(CatalogError.Conflict, "The SKU or barcode is already used by another product in this organization.");
        }

        return CatalogResult<ProductResponse>.Success(ToResponse(product));
    }

    public Task<CatalogResult<bool>> DeactivateProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return DeactivateAsync(
            "products.deactivate",
            () => dbContext.Products.SingleOrDefaultAsync(
                product => product.Id == productId && product.OrganizationId == currentUser.OrganizationId,
                cancellationToken),
            product => product.SetActive(false),
            cancellationToken);
    }

    public async Task<CatalogResult<IReadOnlyList<CategoryResponse>>> ListCategoriesAsync(bool? active, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("categories.view", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<IReadOnlyList<CategoryResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var query = dbContext.Categories.Where(category => category.OrganizationId == currentUser.OrganizationId);
        if (active is bool activeValue)
        {
            query = query.Where(category => category.IsActive == activeValue);
        }

        var categories = await query.OrderBy(category => category.NormalizedName).ToListAsync(cancellationToken);
        return CatalogResult<IReadOnlyList<CategoryResponse>>.Success(categories.Select(ToResponse).ToList());
    }

    public async Task<CatalogResult<CategoryResponse>> CreateCategoryAsync(CategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("categories.create", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<CategoryResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Invalid, "A category request is required.");
        }

        Category category;
        try
        {
            category = new Category(currentUser.OrganizationId!.Value, request.Name, request.Description);
        }
        catch (DomainException exception)
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.Categories.AnyAsync(existing => existing.OrganizationId == category.OrganizationId && existing.NormalizedName == category.NormalizedName, cancellationToken))
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Conflict, "A category with this name already exists in the organization.");
        }

        dbContext.Categories.Add(category);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Conflict, "A category with this name already exists in the organization.");
        }

        return CatalogResult<CategoryResponse>.Success(ToResponse(category));
    }

    public async Task<CatalogResult<CategoryResponse>> UpdateCategoryAsync(Guid categoryId, CategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("categories.update", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<CategoryResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Invalid, "A category request is required.");
        }

        var category = await dbContext.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == categoryId && candidate.OrganizationId == currentUser.OrganizationId,
            cancellationToken);
        if (category is null)
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.NotFound, "Category not found.");
        }

        try
        {
            category.Update(request.Name, request.Description);
        }
        catch (DomainException exception)
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.Categories.AnyAsync(existing => existing.Id != category.Id && existing.OrganizationId == category.OrganizationId && existing.NormalizedName == category.NormalizedName, cancellationToken))
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Conflict, "A category with this name already exists in the organization.");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<CategoryResponse>.Failure(CatalogError.Conflict, "A category with this name already exists in the organization.");
        }

        return CatalogResult<CategoryResponse>.Success(ToResponse(category));
    }

    public Task<CatalogResult<bool>> DeactivateCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return DeactivateAsync(
            "categories.deactivate",
            () => dbContext.Categories.SingleOrDefaultAsync(
                category => category.Id == categoryId && category.OrganizationId == currentUser.OrganizationId,
                cancellationToken),
            category => category.SetActive(false),
            cancellationToken);
    }

    public async Task<CatalogResult<IReadOnlyList<BrandResponse>>> ListBrandsAsync(bool? active, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("brands.view", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<IReadOnlyList<BrandResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var query = dbContext.Brands.Where(brand => brand.OrganizationId == currentUser.OrganizationId);
        if (active is bool activeValue)
        {
            query = query.Where(brand => brand.IsActive == activeValue);
        }

        var brands = await query.OrderBy(brand => brand.NormalizedName).ToListAsync(cancellationToken);
        return CatalogResult<IReadOnlyList<BrandResponse>>.Success(brands.Select(ToResponse).ToList());
    }

    public async Task<CatalogResult<BrandResponse>> CreateBrandAsync(BrandUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("brands.create", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<BrandResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Invalid, "A brand request is required.");
        }

        Brand brand;
        try
        {
            brand = new Brand(currentUser.OrganizationId!.Value, request.Name, request.Description);
        }
        catch (DomainException exception)
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.Brands.AnyAsync(existing => existing.OrganizationId == brand.OrganizationId && existing.NormalizedName == brand.NormalizedName, cancellationToken))
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Conflict, "A brand with this name already exists in the organization.");
        }

        dbContext.Brands.Add(brand);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Conflict, "A brand with this name already exists in the organization.");
        }

        return CatalogResult<BrandResponse>.Success(ToResponse(brand));
    }

    public async Task<CatalogResult<BrandResponse>> UpdateBrandAsync(Guid brandId, BrandUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("brands.update", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<BrandResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Invalid, "A brand request is required.");
        }

        var brand = await dbContext.Brands.SingleOrDefaultAsync(
            candidate => candidate.Id == brandId && candidate.OrganizationId == currentUser.OrganizationId,
            cancellationToken);
        if (brand is null)
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.NotFound, "Brand not found.");
        }

        try
        {
            brand.Update(request.Name, request.Description);
        }
        catch (DomainException exception)
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.Brands.AnyAsync(existing => existing.Id != brand.Id && existing.OrganizationId == brand.OrganizationId && existing.NormalizedName == brand.NormalizedName, cancellationToken))
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Conflict, "A brand with this name already exists in the organization.");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<BrandResponse>.Failure(CatalogError.Conflict, "A brand with this name already exists in the organization.");
        }

        return CatalogResult<BrandResponse>.Success(ToResponse(brand));
    }

    public Task<CatalogResult<bool>> DeactivateBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return DeactivateAsync(
            "brands.deactivate",
            () => dbContext.Brands.SingleOrDefaultAsync(
                brand => brand.Id == brandId && brand.OrganizationId == currentUser.OrganizationId,
                cancellationToken),
            brand => brand.SetActive(false),
            cancellationToken);
    }

    public async Task<CatalogResult<IReadOnlyList<UnitOfMeasureResponse>>> ListUnitsAsync(bool? active, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("units.view", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<IReadOnlyList<UnitOfMeasureResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var query = dbContext.UnitsOfMeasure.Where(unit => unit.OrganizationId == currentUser.OrganizationId);
        if (active is bool activeValue)
        {
            query = query.Where(unit => unit.IsActive == activeValue);
        }

        var units = await query.OrderBy(unit => unit.Code).ToListAsync(cancellationToken);
        return CatalogResult<IReadOnlyList<UnitOfMeasureResponse>>.Success(units.Select(ToResponse).ToList());
    }

    public async Task<CatalogResult<UnitOfMeasureResponse>> CreateUnitAsync(UnitOfMeasureUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("units.create", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Invalid, "A unit request is required.");
        }

        UnitOfMeasure unit;
        try
        {
            unit = new UnitOfMeasure(currentUser.OrganizationId!.Value, request.Code, request.Name, request.Symbol);
        }
        catch (DomainException exception)
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.UnitsOfMeasure.AnyAsync(existing => existing.OrganizationId == unit.OrganizationId && existing.Code == unit.Code, cancellationToken))
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Conflict, "A unit with this code already exists in the organization.");
        }

        dbContext.UnitsOfMeasure.Add(unit);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Conflict, "A unit with this code already exists in the organization.");
        }

        return CatalogResult<UnitOfMeasureResponse>.Success(ToResponse(unit));
    }

    public async Task<CatalogResult<UnitOfMeasureResponse>> UpdateUnitAsync(Guid unitId, UnitOfMeasureUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("units.update", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Invalid, "A unit request is required.");
        }

        var unit = await dbContext.UnitsOfMeasure.SingleOrDefaultAsync(
            candidate => candidate.Id == unitId && candidate.OrganizationId == currentUser.OrganizationId,
            cancellationToken);
        if (unit is null)
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.NotFound, "Unit not found.");
        }

        try
        {
            unit.Update(request.Code, request.Name, request.Symbol);
        }
        catch (DomainException exception)
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.UnitsOfMeasure.AnyAsync(existing => existing.Id != unit.Id && existing.OrganizationId == unit.OrganizationId && existing.Code == unit.Code, cancellationToken))
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Conflict, "A unit with this code already exists in the organization.");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<UnitOfMeasureResponse>.Failure(CatalogError.Conflict, "A unit with this code already exists in the organization.");
        }

        return CatalogResult<UnitOfMeasureResponse>.Success(ToResponse(unit));
    }

    public Task<CatalogResult<bool>> DeactivateUnitAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        return DeactivateAsync(
            "units.deactivate",
            () => dbContext.UnitsOfMeasure.SingleOrDefaultAsync(
                unit => unit.Id == unitId && unit.OrganizationId == currentUser.OrganizationId,
                cancellationToken),
            unit => unit.SetActive(false),
            cancellationToken);
    }

    public async Task<CatalogResult<IReadOnlyList<TaxCategoryResponse>>> ListTaxCategoriesAsync(bool? active, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("tax_categories.view", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<IReadOnlyList<TaxCategoryResponse>>.Failure(authorization.Value, Message(authorization.Value));
        }

        var query = dbContext.TaxCategories.Where(taxCategory => taxCategory.OrganizationId == currentUser.OrganizationId);
        if (active is bool activeValue)
        {
            query = query.Where(taxCategory => taxCategory.IsActive == activeValue);
        }

        var taxCategories = await query.OrderBy(taxCategory => taxCategory.Code).ToListAsync(cancellationToken);
        return CatalogResult<IReadOnlyList<TaxCategoryResponse>>.Success(taxCategories.Select(ToResponse).ToList());
    }

    public async Task<CatalogResult<TaxCategoryResponse>> CreateTaxCategoryAsync(TaxCategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("tax_categories.create", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<TaxCategoryResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Invalid, "A tax category request is required.");
        }

        TaxCategory taxCategory;
        try
        {
            taxCategory = new TaxCategory(currentUser.OrganizationId!.Value, request.Code, request.Name, request.Rate);
        }
        catch (DomainException exception)
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.TaxCategories.AnyAsync(existing => existing.OrganizationId == taxCategory.OrganizationId && existing.Code == taxCategory.Code, cancellationToken))
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Conflict, "A tax category with this code already exists in the organization.");
        }

        dbContext.TaxCategories.Add(taxCategory);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Conflict, "A tax category with this code already exists in the organization.");
        }

        return CatalogResult<TaxCategoryResponse>.Success(ToResponse(taxCategory));
    }

    public async Task<CatalogResult<TaxCategoryResponse>> UpdateTaxCategoryAsync(Guid taxCategoryId, TaxCategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("tax_categories.update", cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<TaxCategoryResponse>.Failure(authorization.Value, Message(authorization.Value));
        }

        if (request is null)
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Invalid, "A tax category request is required.");
        }

        var taxCategory = await dbContext.TaxCategories.SingleOrDefaultAsync(
            candidate => candidate.Id == taxCategoryId && candidate.OrganizationId == currentUser.OrganizationId,
            cancellationToken);
        if (taxCategory is null)
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.NotFound, "Tax category not found.");
        }

        try
        {
            taxCategory.Update(request.Code, request.Name, request.Rate);
        }
        catch (DomainException exception)
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Invalid, exception.Message);
        }

        if (await dbContext.TaxCategories.AnyAsync(existing => existing.Id != taxCategory.Id && existing.OrganizationId == taxCategory.OrganizationId && existing.Code == taxCategory.Code, cancellationToken))
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Conflict, "A tax category with this code already exists in the organization.");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogResult<TaxCategoryResponse>.Failure(CatalogError.Conflict, "A tax category with this code already exists in the organization.");
        }

        return CatalogResult<TaxCategoryResponse>.Success(ToResponse(taxCategory));
    }

    public Task<CatalogResult<bool>> DeactivateTaxCategoryAsync(Guid taxCategoryId, CancellationToken cancellationToken = default)
    {
        return DeactivateAsync(
            "tax_categories.deactivate",
            () => dbContext.TaxCategories.SingleOrDefaultAsync(
                taxCategory => taxCategory.Id == taxCategoryId && taxCategory.OrganizationId == currentUser.OrganizationId,
                cancellationToken),
            taxCategory => taxCategory.SetActive(false),
            cancellationToken);
    }

    private async Task<CatalogError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
        {
            return CatalogError.Unauthenticated;
        }

        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null
            : CatalogError.Forbidden;
    }

    private async Task<CatalogError?> ValidateProductReferencesAsync(
        Guid organizationId,
        ProductUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CategoryId is Guid categoryId)
        {
            var category = await dbContext.Categories.SingleOrDefaultAsync(
                candidate => candidate.Id == categoryId && candidate.OrganizationId == organizationId,
                cancellationToken);
            if (category is null)
            {
                return CatalogError.NotFound;
            }

            if (!category.IsActive)
            {
                return CatalogError.Conflict;
            }
        }

        if (request.BrandId is Guid brandId)
        {
            var brand = await dbContext.Brands.SingleOrDefaultAsync(
                candidate => candidate.Id == brandId && candidate.OrganizationId == organizationId,
                cancellationToken);
            if (brand is null)
            {
                return CatalogError.NotFound;
            }

            if (!brand.IsActive)
            {
                return CatalogError.Conflict;
            }
        }

        var unit = await dbContext.UnitsOfMeasure.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UnitOfMeasureId && candidate.OrganizationId == organizationId,
            cancellationToken);
        if (unit is null)
        {
            return CatalogError.NotFound;
        }

        if (!unit.IsActive)
        {
            return CatalogError.Conflict;
        }

        var taxCategory = await dbContext.TaxCategories.SingleOrDefaultAsync(
            candidate => candidate.Id == request.TaxCategoryId && candidate.OrganizationId == organizationId,
            cancellationToken);
        if (taxCategory is null)
        {
            return CatalogError.NotFound;
        }

        return taxCategory.IsActive ? null : CatalogError.Conflict;
    }

    private Task<bool> ProductIdentityExistsAsync(
        Guid organizationId,
        string sku,
        string? barcode,
        Guid? exceptProductId,
        CancellationToken cancellationToken)
    {
        return dbContext.Products.AnyAsync(product => product.OrganizationId == organizationId &&
            (!exceptProductId.HasValue || product.Id != exceptProductId.Value) &&
            (product.Sku == sku || barcode != null && product.Barcode == barcode), cancellationToken);
    }

    private async Task<CatalogResult<bool>> DeactivateAsync<TEntity>(
        string permission,
        Func<Task<TEntity?>> findEntity,
        Action<TEntity> deactivate,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var authorization = await AuthorizeAsync(permission, cancellationToken);
        if (authorization is not null)
        {
            return CatalogResult<bool>.Failure(authorization.Value, Message(authorization.Value));
        }

        var entity = await findEntity();
        if (entity is null)
        {
            return CatalogResult<bool>.Failure(CatalogError.NotFound, "Catalog resource not found.");
        }

        deactivate(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CatalogResult<bool>.Success(true);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    private static string Message(CatalogError error)
    {
        return error == CatalogError.Unauthenticated
            ? "Authentication is required."
            : "The required catalog permission is missing.";
    }

    private static string ReferenceMessage(CatalogError error)
    {
        return error switch
        {
            CatalogError.NotFound => "A product reference was not found in the authenticated organization.",
            CatalogError.Conflict => "Inactive catalog references cannot be used by a product.",
            _ => "Invalid product references."
        };
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Sku,
            product.Barcode,
            product.Name,
            product.Description,
            product.CategoryId,
            product.BrandId,
            product.UnitOfMeasureId,
            product.TaxCategoryId,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt);
    }

    private static CategoryResponse ToResponse(Category category)
    {
        return new CategoryResponse(category.Id, category.Name, category.Description, category.IsActive, category.CreatedAt, category.UpdatedAt);
    }

    private static BrandResponse ToResponse(Brand brand)
    {
        return new BrandResponse(brand.Id, brand.Name, brand.Description, brand.IsActive, brand.CreatedAt, brand.UpdatedAt);
    }

    private static UnitOfMeasureResponse ToResponse(UnitOfMeasure unit)
    {
        return new UnitOfMeasureResponse(unit.Id, unit.Code, unit.Name, unit.Symbol, unit.IsActive, unit.CreatedAt, unit.UpdatedAt);
    }

    private static TaxCategoryResponse ToResponse(TaxCategory taxCategory)
    {
        return new TaxCategoryResponse(taxCategory.Id, taxCategory.Code, taxCategory.Name, taxCategory.Rate, taxCategory.IsActive, taxCategory.CreatedAt, taxCategory.UpdatedAt);
    }
}