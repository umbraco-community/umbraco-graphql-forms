using Our.Umbraco.GraphQL.Attributes;
using Our.Umbraco.GraphQL.Types;
using System;
using System.Collections.Generic;
using Umbraco.Forms.Core.Interfaces;
using Umbraco.Forms.Core.Services;

namespace Our.Umbraco.GraphQL.Forms.Types
{
    public class WorkflowsDataQuery
    {
        public IEnumerable<IWorkflow> All([Inject] IWorkflowService workflowService) => workflowService.Get();

        public IWorkflow ById([Inject] IWorkflowService workflowService, Id id) => Guid.TryParse(id.Value, out var guid) ? workflowService.Get(guid) : null;
    }
}
