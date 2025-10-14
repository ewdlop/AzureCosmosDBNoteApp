using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Shared
{
    public class PatchOperationDynamic(PatchOperationType operationType, string path, dynamic value) : PatchOperation<dynamic>
    {
        protected dynamic _value = value;

        public override dynamic Value => _value;

        public override string Path => path;

        public override PatchOperationType OperationType => operationType;

        public override bool TrySerializeValueParameter(
                CosmosSerializer cosmosSerializer,
                out Stream? valueParam)
        {
            try
            {
                valueParam = cosmosSerializer.ToStream(_value);
                return true;
            }
            catch (Exception)
            {
                valueParam = null;
                return false;
            }

        }
    }
    public class CustomPatchOperation<T>(PatchOperationType operationType, string path, T? value) : PatchOperation<T>
    {

        public CustomPatchOperation(PatchOperationType operationType, string path) : this(operationType, path, default(T)) { }
        public CustomPatchOperation(PatchOperationType operationType, string from, string path) : this(operationType, path, default(T))
        {
            From = from;
        }

        public static CustomPatchOperation<T> Add(
            string path,
            T value)
        {
            return new CustomPatchOperation<T>(
                PatchOperationType.Add,
                path,
                value);
        }

        public static new CustomPatchOperation<T> Remove(string path)
        {
            return new CustomPatchOperation<T>(
                PatchOperationType.Remove,
                path);
        }

        public static CustomPatchOperation<T> Replace(
            string path,
            T value)
        {
            
            return new CustomPatchOperation<T>(
                PatchOperationType.Replace,
                path,
                value);
        }
        public static CustomPatchOperation<T> Set(
            string path,
            T value)
        {
            return new CustomPatchOperation<T>(
                PatchOperationType.Set,
                path,
                value);
        }
        public static new CustomPatchOperation<long> Increment(
            string path,
            long value)
        {
            return new CustomPatchOperation<long>(
                PatchOperationType.Increment,
                path,
                value);
        }
        public static new CustomPatchOperation<double> Increment(
            string path,
            double value)
        {
            return new CustomPatchOperation<double>(
                PatchOperationType.Increment,
                path,
                value);
        }
        public static new CustomPatchOperation<string> Move(
            string from,
            string path)
        {
            return new CustomPatchOperation<string>(
                PatchOperationType.Move,
                path,
                from);
        }

        protected T? _value = value;

        public override T? Value => _value;

        public override string Path => path;

        public override PatchOperationType OperationType => operationType;

        public override bool TrySerializeValueParameter(
                CosmosSerializer cosmosSerializer,
                out Stream? valueParam)
        {
            try
            {
                valueParam = cosmosSerializer.ToStream(_value);
                return true;
            }
            catch (Exception)
            {
                valueParam = null;
                return false;
            }
        }
    }
}