using Microsoft.Extensions.DependencyInjection;
using Our.Umbraco.GraphQL.Forms.Types;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Our.Umbraco.GraphQL.Forms.Composing
{
    public class GraphQLFormsComposer : ComponentComposer<GraphQLFormsComponent>, IUserComposer
    {
        public override void Compose(IUmbracoBuilder builder)
        {
            base.Compose(builder);

            builder.Services.AddTransient<FormGraphType>();
            builder.Services.AddTransient<PageGraphType>();
            builder.Services.AddTransient<FormDataSourceDefinitionGraphType>();
            builder.Services.AddTransient<FieldSetGraphType>();
            builder.Services.AddTransient<FormDataSourceMappingGraphType>();
            builder.Services.AddTransient<FieldsetContainerGraphType>();
            builder.Services.AddTransient<FieldConditionGraphType>();
            builder.Services.AddTransient<FieldGraphType>();
            builder.Services.AddTransient<FieldConditionRuleGraphType>();
            builder.Services.AddTransient<StringKeyValuePairGraphType>();
            builder.Services.AddTransient<FormDataSourceGraphType>();
            builder.Services.AddTransient<DataSourcesDataQuery>();
            builder.Services.AddTransient<WorkflowGraphType>();
            builder.Services.AddTransient<FieldPreValueSourceGraphType>();
            builder.Services.AddTransient<FieldValueInputType>();
            builder.Services.AddTransient<ExtendQueryWithUmbracoFormsQuery>();
            builder.Services.AddTransient<ExtendMutationWithUmbracoFormsMutation>();
        }
    }
}
