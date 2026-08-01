namespace ComputeWeave.Interop;

internal enum ExternalDomainReference : byte
{
    Owner = 0,
    ResourceSet = 1,
    PersistentLease = 2,
    TransientOperation = 3,
    PendingTransaction = 4,
    Maintenance = 5
}
