using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Engine.Models;
using Pivot.Repositories;

namespace Pivot.Services
{
    /// <summary>
    /// MetadataService - bridges to IAssetRepository for backward compatibility.
    /// Now returns AssetEntity directly (unified model).
    /// </summary>
    public class MetadataService
    {
        private readonly IServiceProvider _serviceProvider;

        public MetadataService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Streams AssetEntity objects from the database using IAssetRepository.
        /// </summary>
        public async IAsyncEnumerable<AssetEntity> StreamAssetsAsync(
            int batchSize = 100,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Create a scope for the scoped IAssetRepository
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetService<IAssetRepository>();
            
            if (repository == null)
            {
                yield break;
            }

            int skip = 0;
            bool hasMore = true;

            while (hasMore && !cancellationToken.IsCancellationRequested)
            {
                var entities = await repository.GetPagedAsync(
                    kind: null, 
                    directory: null, 
                    sortField: "LastModifiedUtc", 
                    ascending: false, 
                    skip: skip, 
                    take: batchSize, 
                    ct: cancellationToken);
                
                int count = 0;

                foreach (var entity in entities)
                {
                    count++;
                    yield return entity;
                }

                hasMore = count == batchSize;
                skip += batchSize;
            }
        }

        /// <summary>
        /// Gets total count of assets matching the filter.
        /// </summary>
        public async Task<int> GetAssetCountAsync(
            AssetKind? kind = null,
            string? directory = null,
            CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetService<IAssetRepository>();
            
            if (repository == null)
            {
                return 0;
            }

            return await repository.GetCountAsync(kind, directory, cancellationToken);
        }

        /// <summary>
        /// Gets assets with paging support.
        /// </summary>
        public async Task<List<AssetEntity>> GetPagedAssetsAsync(
            int skip = 0,
            int take = 100,
            AssetKind? kind = null,
            string? directory = null,
            string sortField = "LastModifiedUtc",
            bool ascending = false,
            CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetService<IAssetRepository>();
            
            if (repository == null)
            {
                return new List<AssetEntity>();
            }

            return await repository.GetPagedAsync(kind, directory, sortField, ascending, skip, take, cancellationToken);
        }
    }
}
