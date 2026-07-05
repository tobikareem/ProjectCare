using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace CarePath.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core unit of work implementation for CarePath repositories and transaction boundaries.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CarePathDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    private IRepository<User>? _users;
    private IRepository<Caregiver>? _caregivers;
    private IRepository<CaregiverCertification>? _caregiverCertifications;
    private IRepository<Client>? _clients;
    private IRepository<ClientAccessGrant>? _clientAccessGrants;
    private IRepository<CarePlan>? _carePlans;
    private IRepository<Shift>? _shifts;
    private IRepository<VisitNote>? _visitNotes;
    private IRepository<VisitPhoto>? _visitPhotos;
    private IRepository<Invoice>? _invoices;
    private IRepository<InvoiceLineItem>? _invoiceLineItems;
    private IRepository<Payment>? _payments;

    /// <summary>Initializes a new unit of work over a CarePath database context.</summary>
    /// <param name="context">CarePath database context.</param>
    public UnitOfWork(CarePathDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public IRepository<User> Users => _users ??= new Repository<User>(_context);

    /// <inheritdoc />
    public IRepository<Caregiver> Caregivers => _caregivers ??= new Repository<Caregiver>(_context);

    /// <inheritdoc />
    public IRepository<CaregiverCertification> CaregiverCertifications =>
        _caregiverCertifications ??= new Repository<CaregiverCertification>(_context);

    /// <inheritdoc />
    public IRepository<Client> Clients => _clients ??= new Repository<Client>(_context);

    /// <inheritdoc />
    public IRepository<ClientAccessGrant> ClientAccessGrants =>
        _clientAccessGrants ??= new Repository<ClientAccessGrant>(_context);

    /// <inheritdoc />
    public IRepository<CarePlan> CarePlans => _carePlans ??= new Repository<CarePlan>(_context);

    /// <inheritdoc />
    public IRepository<Shift> Shifts => _shifts ??= new Repository<Shift>(_context);

    /// <inheritdoc />
    public IRepository<VisitNote> VisitNotes => _visitNotes ??= new Repository<VisitNote>(_context);

    /// <inheritdoc />
    public IRepository<VisitPhoto> VisitPhotos => _visitPhotos ??= new Repository<VisitPhoto>(_context);

    /// <inheritdoc />
    public IRepository<Invoice> Invoices => _invoices ??= new Repository<Invoice>(_context);

    /// <inheritdoc />
    public IRepository<InvoiceLineItem> InvoiceLineItems =>
        _invoiceLineItems ??= new Repository<InvoiceLineItem>(_context);

    /// <inheritdoc />
    public IRepository<Payment> Payments => _payments ??= new Repository<Payment>(_context);

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active for this unit of work.");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            throw new InvalidOperationException("No active transaction exists for this unit of work.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await DisposeCurrentTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            throw new InvalidOperationException("No active transaction exists for this unit of work.");
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeCurrentTransactionAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _currentTransaction?.Dispose();
        _context.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
        }

        await _context.DisposeAsync();
        _disposed = true;
    }

    private async Task DisposeCurrentTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
