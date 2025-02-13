using Microsoft.Azure.Cosmos;

namespace Shared
{
    public static class PacthOperationExtension
    {
#if false
        public static PatchOperation<T> ToGenericPatchOperation<T>(this PatchOperation patchOperation, CosmsDBS
        {
            throw new System.NotImplementedException();
        }
#endif

        public static PatchOperation ToPatchOperation<T>(this PatchOperation<T> patchOperation)
        {
            return patchOperation.OperationType switch
            {
                PatchOperationType.Add => PatchOperation.Add(patchOperation.Path, patchOperation.Value),
                PatchOperationType.Remove => PatchOperation.Remove(patchOperation.Path),
                PatchOperationType.Replace => PatchOperation.Replace(patchOperation.Path, patchOperation.Value),
                PatchOperationType.Set => PatchOperation.Set(patchOperation.Path, patchOperation.Value),
                PatchOperationType.Increment => patchOperation switch
                {
                    PatchOperation<long> longPatchOperation => PatchOperation.Increment(patchOperation.Path, longPatchOperation.Value),
                    PatchOperation<double> doublePatchOperation => PatchOperation.Increment(patchOperation.Path, doublePatchOperation.Value),
                    _ => throw new NotSupportedException("Unsupported PatchOperation type")
                },
                _ => throw new NotSupportedException("Unsupported PatchOperation type")
            };
        }
    }
}