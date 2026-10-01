using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Purchasing;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.Purchasing;
using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.Infrastructure.Purchasing;

public sealed class SupplierInvoiceService(SmartShopPosDbContext db, ICurrentUser currentUser,
    IPermissionChecker permissions, IBranchAccessService branchAccess) : ISupplierInvoiceService
{
    public async Task<SupplierInvoiceResult<IReadOnlyList<SupplierInvoiceSummaryResponse>>> ListAsync(CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.view", token);
        if (auth is not null) return Fail<IReadOnlyList<SupplierInvoiceSummaryResponse>>(auth.Value);
        var rows = await db.SupplierInvoices.AsNoTracking().Where(x => x.OrganizationId == currentUser.OrganizationId)
            .OrderByDescending(x => x.InvoiceDate).ThenBy(x => x.InternalNumber)
            .Select(x => new SupplierInvoiceSummaryResponse(x.Id, x.InternalNumber, x.SupplierId, x.Supplier.Name,
                x.PurchaseOrderId, x.InvoiceNumber, x.InvoiceDate, x.Status.ToString(), x.GrossAmount, x.CreatedAt))
            .ToListAsync(token);
        return SupplierInvoiceResult<IReadOnlyList<SupplierInvoiceSummaryResponse>>.Success(rows);
    }

    public async Task<SupplierInvoiceResult<SupplierInvoiceResponse>> GetAsync(Guid id, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.view", token);
        if (auth is not null) return Fail<SupplierInvoiceResponse>(auth.Value);
        var invoice = await LoadAsync(id, token);
        if (invoice is null) return Missing<SupplierInvoiceResponse>();
        if (!await CheckOrderAccessAsync(invoice.PurchaseOrderId, "supplier_invoices.view", token)) return Denied<SupplierInvoiceResponse>();
        return SupplierInvoiceResult<SupplierInvoiceResponse>.Success(ToResponse(invoice));
    }

    public async Task<SupplierInvoiceResult<SupplierInvoiceResponse>> CreateAsync(SupplierInvoiceCreateRequest request, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.create", token);
        if (auth is not null) return Fail<SupplierInvoiceResponse>(auth.Value);
        var invalid = ValidateHeader(request.SupplierId, request.PurchaseOrderId, request.InvoiceNumber, request.InvoiceDate, request.DueDate,
            request.SupplierDocumentNetAmount, request.SupplierDocumentVatAmount, request.OtherTaxAmount, request.SupplierDocumentGrossAmount);
        if (invalid is not null) return Invalid<SupplierInvoiceResponse>(invalid);
        var org = currentUser.OrganizationId!.Value;
        var normalized = SupplierInvoiceNumber.NormalizeComparisonValue(request.InvoiceNumber);
        if (normalized.Length > 120) return Invalid<SupplierInvoiceResponse>("Normalized supplier invoice number exceeds 120 characters.");
        SupplierInvoice? createdInvoice = null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        try
        {
            var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId && x.OrganizationId == org, token);
            if (supplier is null) return Missing<SupplierInvoiceResponse>("Supplier not found.");
            if (!supplier.IsActive) return Conflict<SupplierInvoiceResponse>("Inactive suppliers cannot be invoiced.");
            if (request.PurchaseOrderId is Guid poId)
            {
                var order = await db.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == poId && x.OrganizationId == org, token);
                if (order is null) return Missing<SupplierInvoiceResponse>("Purchase order not found.");
                if (order.SupplierId != supplier.Id) return Invalid<SupplierInvoiceResponse>("The purchase order supplier must match the invoice supplier.");
                if (order.Status != PurchaseOrderStatus.Submitted) return Conflict<SupplierInvoiceResponse>("Only submitted purchase orders can be invoiced.");
                if (!await branchAccess.CanOperateInBranchAsync(order.BranchId, "supplier_invoices.create", token)) return Denied<SupplierInvoiceResponse>();
            }
            var number = await NextNumberAsync(org, token);
            var invoice = new SupplierInvoice(org, supplier.Id, request.PurchaseOrderId, request.InvoiceNumber, normalized,
                $"SI-{number.ToString("D6", CultureInfo.InvariantCulture)}", request.InvoiceDate, request.DueDate,
                request.SupplierDocumentNetAmount, request.SupplierDocumentVatAmount, request.OtherTaxAmount,
                request.SupplierDocumentGrossAmount, request.Notes, currentUser.UserId!.Value);
            db.SupplierInvoices.Add(invoice);
            createdInvoice = invoice;
            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
            return SupplierInvoiceResult<SupplierInvoiceResponse>.Success(ToResponse(invoice, supplier.Name));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(token);
            if (createdInvoice is not null) db.Entry(createdInvoice).State = EntityState.Detached;
            return Conflict<SupplierInvoiceResponse>("The supplier invoice number already exists for this supplier, or internal numbering conflicted.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await tx.RollbackAsync(token); return Conflict<SupplierInvoiceResponse>("A concurrent invoice operation conflicted. Retry the request.");
        }
    }

    public async Task<SupplierInvoiceResult<SupplierInvoiceResponse>> UpdateAsync(Guid id, SupplierInvoiceUpdateRequest request, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.update", token);
        if (auth is not null) return Fail<SupplierInvoiceResponse>(auth.Value);
        var invalid = ValidateHeader(Guid.NewGuid(), null, request.InvoiceNumber, request.InvoiceDate, request.DueDate,
            request.SupplierDocumentNetAmount, request.SupplierDocumentVatAmount, request.OtherTaxAmount, request.SupplierDocumentGrossAmount);
        if (invalid is not null) return Invalid<SupplierInvoiceResponse>(invalid);
        var normalized = SupplierInvoiceNumber.NormalizeComparisonValue(request.InvoiceNumber);
        if (normalized.Length > 120) return Invalid<SupplierInvoiceResponse>("Normalized supplier invoice number exceeds 120 characters.");
        var invoice = await LoadAsync(id, token);
        if (invoice is null) return Missing<SupplierInvoiceResponse>();
        if (!await CheckOrderAccessAsync(invoice.PurchaseOrderId, "supplier_invoices.update", token)) return Denied<SupplierInvoiceResponse>();
        try { invoice.UpdateDraft(request.InvoiceNumber, normalized, request.InvoiceDate, request.DueDate,
            request.SupplierDocumentNetAmount, request.SupplierDocumentVatAmount, request.OtherTaxAmount, request.SupplierDocumentGrossAmount, request.Notes); }
        catch (DomainException ex) { return Conflict<SupplierInvoiceResponse>(ex.Message); }
        try
        {
            Recalculate(invoice);
            await db.SaveChangesAsync(token);
            return SupplierInvoiceResult<SupplierInvoiceResponse>.Success(ToResponse(invoice));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await db.Entry(invoice).ReloadAsync(token);
            return Conflict<SupplierInvoiceResponse>("Supplier invoice number already exists for this supplier.");
        }
    }

    public async Task<SupplierInvoiceResult<SupplierInvoiceLineResponse>> AddLineAsync(Guid id, SupplierInvoiceLineRequest request, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.update", token);
        if (auth is not null) return Fail<SupplierInvoiceLineResponse>(auth.Value);
        var invoice = await LoadAsync(id, token);
        if (invoice is null) return Missing<SupplierInvoiceLineResponse>();
        if (!await CheckOrderAccessAsync(invoice.PurchaseOrderId, "supplier_invoices.update", token)) return Denied<SupplierInvoiceLineResponse>();
        try { invoice.EnsureDraft(); } catch (DomainException ex) { return Conflict<SupplierInvoiceLineResponse>(ex.Message); }
        var validation = await ValidateLineAsync(invoice, request, token);
        if (validation is not null) return Invalid<SupplierInvoiceLineResponse>(validation);
        try
        {
            invoice.EnsureDraft();
            var line = new SupplierInvoiceLine(invoice.OrganizationId, invoice.Id, invoice.PurchaseOrderId, request.PurchaseOrderLineId,
                request.Description, request.Quantity, request.UnitPrice, request.TaxAmount, request.TaxCategoryId);
            invoice.Lines.Add(line);
            db.SupplierInvoiceLines.Add(line);
            Recalculate(invoice); await db.SaveChangesAsync(token);
            return SupplierInvoiceResult<SupplierInvoiceLineResponse>.Success(ToResponse(line));
        }
        catch (DomainException ex) { return Conflict<SupplierInvoiceLineResponse>(ex.Message); }
    }

    public async Task<SupplierInvoiceResult<SupplierInvoiceLineResponse>> UpdateLineAsync(Guid id, Guid lineId, SupplierInvoiceLineRequest request, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.update", token);
        if (auth is not null) return Fail<SupplierInvoiceLineResponse>(auth.Value);
        var invoice = await LoadAsync(id, token);
        if (invoice is null) return Missing<SupplierInvoiceLineResponse>();
        if (!await CheckOrderAccessAsync(invoice.PurchaseOrderId, "supplier_invoices.update", token)) return Denied<SupplierInvoiceLineResponse>();
        try { invoice.EnsureDraft(); } catch (DomainException ex) { return Conflict<SupplierInvoiceLineResponse>(ex.Message); }
        var line = invoice.Lines.SingleOrDefault(x => x.Id == lineId);
        if (line is null) return Missing<SupplierInvoiceLineResponse>("Supplier invoice line not found.");
        var validation = await ValidateLineAsync(invoice, request, token);
        if (validation is not null) return Invalid<SupplierInvoiceLineResponse>(validation);
        try { invoice.EnsureDraft(); line.Update(invoice.PurchaseOrderId, request.PurchaseOrderLineId, request.TaxCategoryId, request.Description, request.Quantity, request.UnitPrice, request.TaxAmount); }
        catch (DomainException ex) { return Conflict<SupplierInvoiceLineResponse>(ex.Message); }
        Recalculate(invoice); await db.SaveChangesAsync(token);
        return SupplierInvoiceResult<SupplierInvoiceLineResponse>.Success(ToResponse(line));
    }

    public async Task<SupplierInvoiceResult<bool>> DeleteLineAsync(Guid id, Guid lineId, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.update", token);
        if (auth is not null) return Fail<bool>(auth.Value);
        var invoice = await LoadAsync(id, token);
        if (invoice is null) return Missing<bool>();
        if (!await CheckOrderAccessAsync(invoice.PurchaseOrderId, "supplier_invoices.update", token)) return Denied<bool>();
        var line = invoice.Lines.SingleOrDefault(x => x.Id == lineId);
        if (line is null) return Missing<bool>("Supplier invoice line not found.");
        try { invoice.EnsureDraft(); } catch (DomainException ex) { return Conflict<bool>(ex.Message); }
        db.SupplierInvoiceLines.Remove(line); invoice.Lines.Remove(line); Recalculate(invoice); await db.SaveChangesAsync(token);
        return SupplierInvoiceResult<bool>.Success(true);
    }

    public async Task<SupplierInvoiceResult<SupplierInvoiceResponse>> PostAsync(Guid id, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.post", token);
        if (auth is not null) return Fail<SupplierInvoiceResponse>(auth.Value);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            var invoice = await LoadAsync(id, token);
            if (invoice is null) return Missing<SupplierInvoiceResponse>();
            if (!await CheckOrderAccessAsync(invoice.PurchaseOrderId, "supplier_invoices.post", token)) return Denied<SupplierInvoiceResponse>();
            if (invoice.Lines.Count == 0) return Invalid<SupplierInvoiceResponse>("An invoice must contain at least one line before posting.");
            try { invoice.EnsureDraft(); } catch (DomainException ex) { return Conflict<SupplierInvoiceResponse>(ex.Message); }
            if (invoice.PurchaseOrderId is null)
                return Conflict<SupplierInvoiceResponse>("Non-PO invoice posting requires the expenditure classification and approval policy, which is not part of this foundation.");
            if (invoice.NetAmount != invoice.SupplierDocumentNetAmount || invoice.TaxAmount != invoice.SupplierDocumentVatAmount ||
                invoice.GrossAmount != invoice.SupplierDocumentGrossAmount)
                return Conflict<SupplierInvoiceResponse>("Calculated invoice totals do not match the supplier document; variance approval is not available in this slice.");
            var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == invoice.SupplierId && x.OrganizationId == invoice.OrganizationId, token);
            if (supplier is null || !supplier.IsActive) return Conflict<SupplierInvoiceResponse>("The invoice supplier is inactive or unavailable.");
            if (invoice.PurchaseOrderId is Guid poId)
            {
                var order = await db.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == poId && x.OrganizationId == invoice.OrganizationId, token);
                if (order is null) return Missing<SupplierInvoiceResponse>("Purchase order not found.");
                if (order.Status != PurchaseOrderStatus.Submitted) return Conflict<SupplierInvoiceResponse>("Only submitted purchase orders can be posted against.");
                foreach (var group in invoice.Lines.GroupBy(x => x.PurchaseOrderLineId!.Value))
                {
                    var poLine = await db.PurchaseOrderLines.SingleOrDefaultAsync(x => x.Id == group.Key && x.OrganizationId == invoice.OrganizationId && x.PurchaseOrderId == poId, token);
                    if (poLine is null) return Invalid<SupplierInvoiceResponse>("An invoice line does not belong to this purchase order.");
                    if (group.Any(x => x.UnitPrice != poLine.UnitCost)) return Conflict<SupplierInvoiceResponse>("Invoice unit price differs from the purchase order; variance approval is not available in this slice.");
                    var priorInvoiced = await (from line in db.SupplierInvoiceLines
                        join posted in db.SupplierInvoices on new { line.OrganizationId, Id = line.SupplierInvoiceId } equals new { posted.OrganizationId, Id = posted.Id }
                        where line.OrganizationId == invoice.OrganizationId && line.PurchaseOrderLineId == poLine.Id && posted.Status == SupplierInvoiceStatus.Posted
                        select line.Quantity).SumAsync(token);
                    var received = await db.GoodsReceiptLines.Where(x => x.OrganizationId == invoice.OrganizationId && x.PurchaseOrderId == poId && x.PurchaseOrderLineId == poLine.Id).SumAsync(x => (decimal?)x.QuantityReceived, token) ?? 0m;
                    if (priorInvoiced + group.Sum(x => x.Quantity) > received || priorInvoiced + group.Sum(x => x.Quantity) > poLine.Quantity)
                        return Conflict<SupplierInvoiceResponse>("Posting would exceed received or ordered quantity; a matching variance approval is required.");
                }
            }
            invoice.Post(currentUser.UserId!.Value);
            await db.SaveChangesAsync(token); await tx.CommitAsync(token);
            return SupplierInvoiceResult<SupplierInvoiceResponse>.Success(ToResponse(invoice));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        { await tx.RollbackAsync(token); return Conflict<SupplierInvoiceResponse>("A concurrent posting conflicted. Retry the request."); }
        catch (DbUpdateException ex) when (IsSerializationFailure(ex))
        { await tx.RollbackAsync(token); return Conflict<SupplierInvoiceResponse>("A concurrent posting conflicted. Retry the request."); }
    }

    public async Task<SupplierInvoiceResult<bool>> CancelAsync(Guid id, CancellationToken token = default)
    {
        var auth = await AuthorizeAsync("supplier_invoices.cancel", token);
        if (auth is not null) return Fail<bool>(auth.Value);
        var invoice = await LoadAsync(id, token);
        if (invoice is null) return Missing<bool>();
        if (!await CheckOrderAccessAsync(invoice.PurchaseOrderId, "supplier_invoices.cancel", token)) return Denied<bool>();
        try { invoice.Cancel(); await db.SaveChangesAsync(token); return SupplierInvoiceResult<bool>.Success(true); }
        catch (DomainException ex) { return Conflict<bool>(ex.Message); }
    }

    private async Task<long> NextNumberAsync(Guid organizationId, CancellationToken token)
    {
        await db.Database.OpenConnectionAsync(token);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "INSERT INTO supplier_invoice_number_sequences (\"OrganizationId\", \"LastNumber\") VALUES (@organizationId, 1) ON CONFLICT (\"OrganizationId\") DO UPDATE SET \"LastNumber\" = supplier_invoice_number_sequences.\"LastNumber\" + 1 RETURNING \"LastNumber\";";
        var parameter = command.CreateParameter(); parameter.ParameterName = "organizationId"; parameter.Value = organizationId; command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private async Task<SupplierInvoice?> LoadAsync(Guid id, CancellationToken token) => await db.SupplierInvoices
        .Include(x => x.Lines).Include(x => x.Supplier).Include(x => x.PurchaseOrder)
        .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == currentUser.OrganizationId, token);

    private async Task<bool> CheckOrderAccessAsync(Guid? purchaseOrderId, string permission, CancellationToken token)
    {
        if (purchaseOrderId is not Guid id) return true;
        var branchId = await db.PurchaseOrders.Where(x => x.OrganizationId == currentUser.OrganizationId && x.Id == id).Select(x => (Guid?)x.BranchId).SingleOrDefaultAsync(token);
        return branchId is Guid branch && await branchAccess.CanOperateInBranchAsync(branch, permission, token);
    }

    private async Task<string?> ValidateLineAsync(SupplierInvoice invoice, SupplierInvoiceLineRequest request, CancellationToken token)
    {
        try
        {
            if (invoice.PurchaseOrderId is null && request.PurchaseOrderLineId is not null) return "Non-PO invoice lines cannot reference purchase order lines.";
            if (invoice.PurchaseOrderId is not null && request.PurchaseOrderLineId is null) return "PO-backed invoice lines must reference a purchase order line.";
            if (request.PurchaseOrderLineId is Guid lineId && !await db.PurchaseOrderLines.AnyAsync(x => x.Id == lineId && x.OrganizationId == invoice.OrganizationId && x.PurchaseOrderId == invoice.PurchaseOrderId, token))
                return "Purchase order line must belong to the invoice purchase order.";
            if (request.TaxCategoryId is Guid taxId && !await db.TaxCategories.AnyAsync(x => x.Id == taxId && x.OrganizationId == invoice.OrganizationId && x.IsActive, token)) return "Active tax category not found.";
            _ = new SupplierInvoiceLine(invoice.OrganizationId, invoice.Id, invoice.PurchaseOrderId, request.PurchaseOrderLineId,
                request.Description, request.Quantity, request.UnitPrice, request.TaxAmount, request.TaxCategoryId);
            return null;
        }
        catch (DomainException ex) { return ex.Message; }
    }

    private void Recalculate(SupplierInvoice invoice)
    {
        var lines = db.Entry(invoice).Collection(x => x.Lines).IsLoaded ? invoice.Lines : [];
        invoice.SetTotals(lines.Sum(x => x.NetAmount), lines.Sum(x => x.TaxAmount));
    }
    private async Task<SupplierInvoiceError?> AuthorizeAsync(string permission, CancellationToken token)
    {
        if (currentUser.UserId is null || currentUser.OrganizationId is null) return SupplierInvoiceError.Unauthenticated;
        return await permissions.HasPermissionAsync(currentUser.UserId.Value, permission, token) ? null : SupplierInvoiceError.Forbidden;
    }
    private static string? ValidateHeader(Guid supplierId, Guid? poId, string number, DateOnly invoiceDate, DateOnly? dueDate,
        decimal documentNet, decimal documentVat, decimal otherTax, decimal documentGross)
    {
        if (supplierId == Guid.Empty || poId == Guid.Empty) return "Supplier and purchase order identifiers must be valid.";
        if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 120) return "Supplier invoice number is required and must not exceed 120 characters.";
        if (invoiceDate == default || dueDate == DateOnly.MinValue) return "Invoice date is required and due date must be valid.";
        if (new[] { documentNet, documentVat, otherTax, documentGross }.Any(x => x < 0m || x != decimal.Round(x, 2) || x >= 10_000_000_000_000_000m))
            return "Supplier document tax and total amounts must be non-negative and fit numeric(18,2).";
        return null;
    }
    private static SupplierInvoiceResponse ToResponse(SupplierInvoice x, string? supplierName = null) => new(x.Id, x.InternalNumber, x.SupplierId,
        supplierName ?? x.Supplier.Name, x.PurchaseOrderId, x.PurchaseOrder?.OrderNumber, x.InvoiceNumber, x.InvoiceDate, x.DueDate,
        x.Status.ToString(), x.NetAmount, x.TaxAmount, x.OtherTaxAmount, x.GrossAmount,
        x.SupplierDocumentNetAmount, x.SupplierDocumentVatAmount, x.OtherTaxAmount, x.SupplierDocumentGrossAmount,
        x.Notes, x.CreatedAt, x.PostedAt,
        x.Lines.OrderBy(l => l.CreatedAt).Select(ToResponse).ToArray());
    private static SupplierInvoiceLineResponse ToResponse(SupplierInvoiceLine x) => new(x.Id, x.PurchaseOrderLineId, x.TaxCategoryId,
        x.Description, x.Quantity, x.UnitPrice, x.NetAmount, x.TaxAmount, x.GrossAmount);
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    private static bool IsSerializationFailure(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };
    private static SupplierInvoiceResult<T> Fail<T>(SupplierInvoiceError error) => SupplierInvoiceResult<T>.Failure(error, error == SupplierInvoiceError.Unauthenticated ? "Authentication required." : "Permission denied.");
    private static SupplierInvoiceResult<T> Missing<T>(string message = "Supplier invoice not found.") => SupplierInvoiceResult<T>.Failure(SupplierInvoiceError.NotFound, message);
    private static SupplierInvoiceResult<T> Denied<T>() => SupplierInvoiceResult<T>.Failure(SupplierInvoiceError.Forbidden, "Branch access is required for this purchase order.");
    private static SupplierInvoiceResult<T> Invalid<T>(string message) => SupplierInvoiceResult<T>.Failure(SupplierInvoiceError.Invalid, message);
    private static SupplierInvoiceResult<T> Conflict<T>(string message) => SupplierInvoiceResult<T>.Failure(SupplierInvoiceError.Conflict, message);
}
