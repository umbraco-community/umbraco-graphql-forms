using Our.Umbraco.GraphQL.Attributes;
using Our.Umbraco.GraphQL.Types;
using System;
using System.Collections.Generic;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Services;

namespace Our.Umbraco.GraphQL.Forms.Types
{
    public class DataSourcesDataQuery
    {
        public IEnumerable<FormDataSource> All([Inject] IDataSourceService dataSourceService) => dataSourceService.Get();

        public FormDataSource ById([Inject] IDataSourceService dataSourceService, Id id) => Guid.TryParse(id.Value, out var guid) ? dataSourceService.Get(guid) : null;
    }
}
