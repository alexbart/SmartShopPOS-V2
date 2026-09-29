using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Suppliers;
using SmartShopPOS.Contracts.Suppliers;
using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Suppliers;

public sealed class SupplierService(
    SmartShopPosDbContext dbContext,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : ISupplierService
{
    public async Task<SupplierResult<IReadOnlyList<SupplierResponse>>> ListAsync(
        SupplierFilter filter, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("suppliers.view", cancellationToken);
        if (authorization is not null)
            return SupplierResult<IReadOnlyList<SupplierResponse>>.Failure(authorization.Value, Message(authorization.Value));

        var query = dbContext.Suppliers.Where(s => s.OrganizationId == currentUser.OrganizationId);
        if (filter.Active is bool active)
            query = query.Where(s => s.IsActive == active);
        var suppliers = await query.OrderBy(s => s.Code).ToListAsync(cancellationToken);
        return SupplierResult<IReadOnlyList<SupplierResponse>>.Success(suppliers.Select(ToResponse).ToList());
    }

    public async Task<SupplierResult<SupplierResponse>> GetAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("suppliers.view", cancellationToken);
        if (authorization is not null)
            return SupplierResult<SupplierResponse>.Failure(authorization.Value, Message(authorization.Value));
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(
            s => s.Id == supplierId && s.OrganizationId == currentUser.OrganizationId, cancellationToken);
        return supplier is null
            ? SupplierResult<SupplierResponse>.Failure(SupplierError.NotFound, "Supplier not found.")
            : SupplierResult<SupplierResponse>.Success(ToResponse(supplier));
    }

    public async Task<SupplierResult<SupplierResponse>> CreateAsync(SupplierUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("suppliers.create", cancellationToken);
        if (authorization is not null)
            return SupplierResult<SupplierResponse>.Failure(authorization.Value, Message(authorization.Value));
        if (request is null)
            return SupplierResult<SupplierResponse>.Failure(SupplierError.Invalid, "A supplier request is required.");

        Supplier supplier;
        try { supplier = ToEntity(currentUser.OrganizationId!.Value, request); }
        catch (DomainException ex) { return SupplierResult<SupplierResponse>.Failure(SupplierError.Invalid, ex.Message); }

        if (await CodeExistsAsync(supplier.OrganizationId, supplier.Code, null, cancellationToken))
            return DuplicateCode();
        dbContext.Suppliers.Add(supplier);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return DuplicateCode(); }
        return SupplierResult<SupplierResponse>.Success(ToResponse(supplier));
    }

    public async Task<SupplierResult<SupplierResponse>> UpdateAsync(Guid supplierId, SupplierUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("suppliers.update", cancellationToken);
        if (authorization is not null)
            return SupplierResult<SupplierResponse>.Failure(authorization.Value, Message(authorization.Value));
        if (request is null)
            return SupplierResult<SupplierResponse>.Failure(SupplierError.Invalid, "A supplier request is required.");

        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(
            s => s.Id == supplierId && s.OrganizationId == currentUser.OrganizationId, cancellationToken);
        if (supplier is null)
            return SupplierResult<SupplierResponse>.Failure(SupplierError.NotFound, "Supplier not found.");
        try { supplier.Update(request.Code, request.Name, request.Description, request.ContactPerson, request.Phone,
            request.Email, request.Address, request.TaxIdentifier, request.BusinessRegistrationNumber); }
        catch (DomainException ex) { return SupplierResult<SupplierResponse>.Failure(SupplierError.Invalid, ex.Message); }

        if (await CodeExistsAsync(supplier.OrganizationId, supplier.Code, supplier.Id, cancellationToken))
            return DuplicateCode();
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return DuplicateCode(); }
        return SupplierResult<SupplierResponse>.Success(ToResponse(supplier));
    }

    public async Task<SupplierResult<bool>> DeactivateAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync("suppliers.deactivate", cancellationToken);
        if (authorization is not null)
            return SupplierResult<bool>.Failure(authorization.Value, Message(authorization.Value));
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(
            s => s.Id == supplierId && s.OrganizationId == currentUser.OrganizationId, cancellationToken);
        if (supplier is null)
            return SupplierResult<bool>.Failure(SupplierError.NotFound, "Supplier not found.");
        supplier.SetActive(false);
        await dbContext.SaveChangesAsync(cancellationToken);
        return SupplierResult<bool>.Success(true);
    }

    private async Task<SupplierError?> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            currentUser.OrganizationId is not Guid organizationId || organizationId == Guid.Empty)
            return SupplierError.Unauthenticated;
        return await permissionChecker.HasPermissionAsync(userId, permission, cancellationToken)
            ? null : SupplierError.Forbidden;
    }

    private Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? exceptId, CancellationToken cancellationToken)
    {
        var query = dbContext.Suppliers.Where(s => s.OrganizationId == organizationId && s.Code == code);
        if (exceptId is Guid id)
            query = query.Where(s => s.Id != id);
        return query.AnyAsync(cancellationToken);
    }

    private static Supplier ToEntity(Guid organizationId, SupplierUpsertRequest request) => new(
        organizationId, request.Code, request.Name, request.Description, request.ContactPerson, request.Phone,
        request.Email, request.Address, request.TaxIdentifier, request.BusinessRegistrationNumber);

    private static SupplierResponse ToResponse(Supplier s) => new(s.Id, s.Code, s.Name, s.Description,
        s.ContactPerson, s.Phone, s.Email, s.Address, s.TaxIdentifier, s.BusinessRegistrationNumber,
        s.IsActive, s.CreatedAt, s.UpdatedAt);

    private static SupplierResult<SupplierResponse> DuplicateCode() =>
        SupplierResult<SupplierResponse>.Failure(SupplierError.Conflict, "Supplier code is already used in this organization.");

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string Message(SupplierError error) => error == SupplierError.Unauthenticated
        ? "Authentication is required." : "The required supplier permission is missing.";
}
