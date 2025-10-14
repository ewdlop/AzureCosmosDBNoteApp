using Microsoft.Azure.Cosmos;
using System.Text;
using System.Text.Json;

namespace Shared
{
    public static class PacthOperationExtension
    {
#if false
        //PatchOperation<T> is not abstract
        public static PatchOperation<T> ToGenericPatchOperation<T>(this PatchOperation patchOperation, CosmsDBS
        {
            throw new System.NotImplementedException();
        }
#endif


#if false
        //PatchOperationCore-is-not-internal
        public static PatchOperationCore<T> ToGenericPatchOperationCore<T>(this PatchOperationDynamic patchOperation)
        {
            throw new System.NotImplementedException();
        }

#endif
        public static CustomPatchOperation<T?> ToCustomPatchOperation<T>(this PatchOperation patchOperation, CosmosSerializer cosmosSerializer)
        {
            return patchOperation.OperationType switch
            {
                PatchOperationType.Add => new CustomPatchOperation<T?>(PatchOperationType.Add, patchOperation.Path, patchOperation.ToValue<T>(cosmosSerializer)),
                PatchOperationType.Remove => new CustomPatchOperation<T?>(PatchOperationType.Remove, patchOperation.Path),
                PatchOperationType.Replace => new CustomPatchOperation<T?>(PatchOperationType.Replace, patchOperation.Path, patchOperation.ToValue<T>(cosmosSerializer)),
                PatchOperationType.Set => new CustomPatchOperation<T?>(PatchOperationType.Set, patchOperation.Path, patchOperation.ToValue<T>(cosmosSerializer)),
                PatchOperationType.Increment => new CustomPatchOperation<T?>(PatchOperationType.Increment, patchOperation.Path, patchOperation.ToValue<T>(cosmosSerializer)),
                PatchOperationType.Move => new CustomPatchOperation<T?>(PatchOperationType.Move, patchOperation.From, patchOperation.Path),
                 _ => throw new NotSupportedException("Unsupported PatchOperation type")
            };
        }

        public static T? ToValue<T>(this PatchOperation patchOperation, CosmosSerializer cosmosSerializer)
        {
            if(patchOperation.TrySerializeValueParameter(cosmosSerializer, out Stream? valueParam))
            {
                T value = cosmosSerializer.FromStream<T>(valueParam);
                return value;
            }
            return default;
        }

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
                    PatchOperation<int> intPatchOperation => PatchOperation.Increment(patchOperation.Path, intPatchOperation.Value),
                    PatchOperation<long> longPatchOperation => PatchOperation.Increment(patchOperation.Path, longPatchOperation.Value),
                    PatchOperation<float> floatPatchOperation => PatchOperation.Increment(patchOperation.Path, floatPatchOperation.Value),
                    PatchOperation<double> doublePatchOperation => PatchOperation.Increment(patchOperation.Path, doublePatchOperation.Value),
                    _ => throw new NotSupportedException("Unsupported PatchOperation type")
                },
                PatchOperationType.Move => PatchOperation.Move(patchOperation.From, patchOperation.Path),
                _ => throw new NotSupportedException("Unsupported PatchOperation type")
            };
        }

    }
}