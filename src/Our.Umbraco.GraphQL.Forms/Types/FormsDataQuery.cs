using Our.Umbraco.GraphQL.Attributes;
using Our.Umbraco.GraphQL.Types;
using System;
using System.Collections.Generic;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Services;

namespace Our.Umbraco.GraphQL.Forms.Types
{
    public class FormsDataQuery
    {
        public IEnumerable<Form> All([Inject] IFormService formService) => formService.Get();

        public Form ById([Inject] IFormService formService, Id id) => Guid.TryParse(id.Value, out var formId) ? formService.Get(formId) : null;

        public Form ByName([Inject] IFormService formService, string name) => formService.Get(name);
    }
}
