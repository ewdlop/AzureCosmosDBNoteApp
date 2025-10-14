using Microsoft.Azure.Cosmos;

namespace Shared
{
    public class PatchOperationDynamic(PatchOperationType operationType, string path, dynamic value) : PatchOperation
    {
        protected dynamic _value = value;

        public dynamic Value => _value;

        public override string Path => path;

        public override PatchOperationType OperationType => operationType;

        public override bool TrySerializeValueParameter(
                CosmosSerializer cosmosSerializer,
                out Stream valueParam)
        {
            valueParam = cosmosSerializer.ToStream(_value);
            return false;
        }
    }
}