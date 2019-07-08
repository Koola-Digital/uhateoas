using System.Configuration;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Routing;

namespace uHateoas.League
{
    public class ContentFinderByNiceUrlWithContentAccept : ContentFinderByNiceUrl
    {
        public override bool TryFindContent(PublishedContentRequest docRequest)
        {
            var route = docRequest.HasDomain ?
                docRequest.UmbracoDomain.RootContentId + DomainHelper.PathRelativeToDomain(docRequest.DomainUri, docRequest.Uri.GetAbsolutePathDecoded()) :
                docRequest.Uri.GetAbsolutePathDecoded();

            var node = FindContent(docRequest, route);

            var templateAlias = GetTemplateAliasByContentAccept();
            if (!string.Equals(templateAlias, "unknown", System.StringComparison.OrdinalIgnoreCase))
            {
                var template = ApplicationContext.Current.Services.FileService.GetTemplate(templateAlias);

                if (template != null)
                {
                    LogHelper.Debug<ContentFinderByNiceUrlWithContentAccept>($"Valid template: \"{templateAlias}\"");
                    if (node != null)
                        docRequest.SetTemplate(template);
                }
                else
                {
                    LogHelper.Warn<ContentFinderByNiceUrlWithContentAccept>($"Not a valid template: \"{templateAlias}\"");
                }
            }

            return node != null;
        }

        private static string GetTemplateAliasByContentAccept()
        {
            var contentType = HttpContext.Current.Request.ContentType;
            string template = null;
            if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.{contentType}"))
                template = ConfigurationManager.AppSettings[$"{UExtensions.AppSettingsPrefix}.Templates.{contentType}"];

            return template ?? "unknown";
        }
    }
}