using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Our.Umbraco.GraphQL.Adapters.Types;
using Our.Umbraco.GraphQL.Attributes;
using Our.Umbraco.GraphQL.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Extensions;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Services;

namespace Our.Umbraco.GraphQL.Forms.Types
{
    public class UmbracoFormsMutation : ObjectGraphType
    {
        private readonly ILogger<UmbracoFormsMutation> _logger;
        private readonly IFormService _formService;
        private readonly IFieldTypeStorage _fieldTypeStorage;
        private readonly IRecordService _recordService;
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly IPublishedRouter _publishedRouter;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemberManager _memberManager;
        private readonly IPlaceholderParsingService _placeholderParsingService;

        public UmbracoFormsMutation(ILogger<UmbracoFormsMutation> logger,
                                    IFormService formService,
                                    IFieldTypeStorage fieldTypeStorage,
                                    IRecordService recordService,
                                    IUmbracoContextFactory umbracoContextFactory,
                                    IPublishedRouter publishedRouter,
                                    IHttpContextAccessor httpContextAccessor,
                                    IMemberManager memberManager,
                                    IPlaceholderParsingService placeholderParsingService)
        {
            Name = nameof(UmbracoFormsMutation);

            Field<JsonGraphType>()
                .Name("submit")
                .Argument<NonNullGraphType<Adapters.Types.IdGraphType>>("formId", "The GUID of the form")
                .Argument<NonNullGraphType<Adapters.Types.IdGraphType>>("umbracoPageId", "The integer ID of the Umbraco page you were on")
                .Argument<ListGraphType<FieldValueInputType>>("fields", "An array of objects representing the field data.  Each object has a 'field' property that is either the GUID or alias of the form field, and a 'value' property that is the field value")
                .ResolveAsync(Submit);
            _logger = logger;
            _formService = formService;
            _fieldTypeStorage = fieldTypeStorage;
            _recordService = recordService;
            _umbracoContextFactory = umbracoContextFactory;
            _publishedRouter = publishedRouter;
            _httpContextAccessor = httpContextAccessor;
            _memberManager = memberManager;
            _placeholderParsingService = placeholderParsingService;
        }

        private async Task<object> Submit(IResolveFieldContext<object> ctx)
        {
            string formIdArg = null;
            string umbracoPageId = null;

            try
            {
                formIdArg = ctx.GetArgument<Id>("formId").Value;
                umbracoPageId = ctx.GetArgument<Id>("umbracoPageId").Value;
                var fieldsList = ctx.GetArgument<List<FieldValue>>("fields");

                if (!Guid.TryParse(formIdArg, out var formId) || !(_formService.Get(formId) is Form form)) return SubmitResult("The form ID specified could not be found");
                if (fieldsList == null || fieldsList.Count == 0) return SubmitResult("You must specify one or more field values");

                var fields = new Dictionary<string, string>(fieldsList.Count);
                fieldsList.ForEach(f => fields[f.Field] = f.Value);

                var context = _httpContextAccessor.HttpContext;
                var errors = ValidateFormState(fields, form, context)?.ToList();
                if (errors != null && errors.Count > 0) return SubmitResult(errors);

                using (var ucRef = _umbracoContextFactory.EnsureUmbracoContext())
                {
                    var uc = ucRef.UmbracoContext;
                    var page = Guid.TryParse(umbracoPageId, out var guid) ? uc.Content.GetById(guid)
                        : (int.TryParse(umbracoPageId, out var id) ? uc.Content.GetById(id)
                        : (UdiParser.TryParse(umbracoPageId, out GuidUdi udi) ? uc.Content.GetById(udi.Guid) : null));
                    if (page == null) return SubmitResult("Could not find the umbracoPageId specified");

                    var url = page.Url();
                    if (string.IsNullOrEmpty(url)) return SubmitResult("The page specified does not have a routable URL to associate with the form request");

                    var builder = await _publishedRouter.CreateRequestAsync(new Uri(new Uri(context.Request.GetDisplayUrl()), url)).ConfigureAwait(false);
                    var request = await _publishedRouter.RouteRequestAsync(builder, new RouteRequestOptions(RouteDirection.Inbound)).ConfigureAwait(false);
                    uc.PublishedRequest = request;

                    var recordId = await SubmitForm(context, formId, form, page.Id, fields).ConfigureAwait(false);
                    return SubmitResult(recordId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not submit form {formId} for Umbraco page {umbracoPageId}", formIdArg, umbracoPageId);
                return SubmitResult("An unspecified error occurred.  Check the Umbraco logs for more details.");
            }
        }

        private async Task<Guid> SubmitForm(HttpContext context, Guid formId, Form form, int umbracoPageId, Dictionary<string, string> fields)
        {
            var record = new Record
            {
                Form = formId,
                State = FormState.Submitted,
                UmbracoPageId = umbracoPageId,
                IP = context.Connection?.RemoteIpAddress?.ToString()
            };

            if (context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var username = context.User.Identity.Name;
                var user = string.IsNullOrWhiteSpace(username) ? null : (await _memberManager.GetUserAsync(context.User).ConfigureAwait(false));

                if (user != null) record.MemberKey = user.Key.ToString();
            }

            foreach (var allField in form.AllFields)
            {
                var inputValues = new object[0];
                if (fields.TryGetValue(allField.Id.ToString(), out var field) || fields.TryGetValue(allField.Alias, out field)) inputValues = new[] { field };

                var fieldValues = _fieldTypeStorage.GetFieldTypeByField(allField).ConvertToRecord(allField, inputValues, _httpContextAccessor.HttpContext).ToArray();

                if (record.RecordFields.TryGetValue(allField.Id, out var recordField))
                {
                    recordField.Values.Clear();
                    recordField.Values.AddRange(fieldValues);
                }
                else
                {
                    recordField = new RecordField(allField);
                    recordField.Values.AddRange(fieldValues);
                    record.RecordFields.Add(allField.Id, recordField);
                }
            }

            _recordService.Submit(record, form);

            return record.UniqueId;
        }

        private JToken SubmitResult(string error) => new JObject
        {
            ["success"] = false,
            ["errors"] = new JArray(new[] { new JObject
            {
                ["error"] = error
            } })
        };

        private JToken SubmitResult(IEnumerable<FieldValue> errors) => new JObject
            {
                ["success"] = false,
                ["errors"] = new JArray(errors.Select(e => new JObject
                {
                    ["field"] = e.Field,
                    ["error"] = e.Value
                }).ToArray())
            };

        private JToken SubmitResult(Guid recordId) => new JObject
            {
                ["success"] = true,
                ["id"] = recordId.ToString()
            };

        private IEnumerable<FieldValue> ValidateFormState(Dictionary<string, string> fields, Form form, HttpContext context)
        {
            var allFields = form.AllFields.ToDictionary(f => f.Id, f => string.Join(", ", f.Values ?? new List<object>()));
            foreach (var page in form.Pages)
            {
                foreach (var fieldSet in page.FieldSets)
                {
                    if (fieldSet.Condition != null && fieldSet.Condition.Enabled && !fieldSet.Condition.IsVisible(form, allFields)) continue;

                    foreach (var formField in fieldSet.Containers.SelectMany(x => x.Fields))
                    {
                        var inputValues = new object[0];
                        if (fields.TryGetValue(formField.Id.ToString(), out var field) || fields.TryGetValue(formField.Alias, out field)) inputValues = new[] { field };

                        var type = _fieldTypeStorage.GetFieldTypeByField(formField);
                        var errors = type.ValidateField(form, formField, inputValues, context, _placeholderParsingService);

                        foreach (string error in errors)
                        {
                            var message = error;
                            if (string.IsNullOrWhiteSpace(message)) message = string.Format((form.InvalidErrorMessage ?? "").ParsePlaceHolders(_placeholderParsingService), formField.Caption);
                            yield return new FieldValue { Field = formField.Alias, Value = message };
                        }
                    }
                }
            }
        }
    }

    public class ExtendMutationWithUmbracoFormsMutation
    {
        [NonNull]
        [Description("Mutation to submit an Umbraco Form")]
        public UmbracoFormsMutation UmbracoForms([Inject] UmbracoFormsMutation mutation) => mutation;
    }
}
