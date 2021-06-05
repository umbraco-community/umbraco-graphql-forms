using Our.Umbraco.GraphQL.Attributes;
using Our.Umbraco.GraphQL.Types;
using System;
using System.Collections.Generic;
using Umbraco.Forms.Core.Interfaces;
using Umbraco.Forms.Core.Services;

namespace Our.Umbraco.GraphQL.Forms.Types
{
    public class PreValueSourcesDataQuery
    {
        public IEnumerable<IFieldPreValueSource> All([Inject] IPrevalueSourceService prevalueSourceService) => prevalueSourceService.Get();

        public IFieldPreValueSource ById([Inject] IPrevalueSourceService prevalueSourceService, Id id) => Guid.TryParse(id.Value, out var guid) ? prevalueSourceService.Get(guid) : null;
    }
}
