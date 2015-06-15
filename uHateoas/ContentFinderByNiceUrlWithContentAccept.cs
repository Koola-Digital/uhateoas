using System.Configuration;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web.Routing;

namespace wg2k.umbraco
{
    public class ContentFinderByNiceUrlWithContentAccept : ContentFinderByNiceUrl
    {
        public override bool TryFindContent(PublishedContentRequest docRequest)
        {
            string route = string.Empty;
            if (docRequest.HasDomain)
                route = docRequest.Domain.RootNodeId.ToString() + DomainHelper.PathRelativeToDomain(docRequest.DomainUri, docRequest.Uri.GetAbsolutePathDecoded());
            else
                route = docRequest.Uri.GetAbsolutePathDecoded();

            var node = FindContent(docRequest, route);
            string templateAlias = GetTemplateAliasByContentAccept(docRequest);
            ITemplate template = ApplicationContext.Current.Services.FileService.GetTemplate(templateAlias);
            if (template != null)
            {
                LogHelper.Debug<ContentFinderByNiceUrlWithContentAccept>("Valid template: \"{0}\"", () => templateAlias);
                if (node != null)
                    docRequest.SetTemplate(template);
            }
            else
            {
                LogHelper.Debug<ContentFinderByNiceUrlWithContentAccept>("Not a valid template: \"{0}\"", () => templateAlias);
            }
            return node != null;
        }
        private string GetTemplateAliasByContentAccept(PublishedContentRequest docRequest)
        {
            HttpContext context = HttpContext.Current;
            string template = null;
            if (ConfigurationManager.AppSettings.AllKeys.Contains("WG2K.Hypermedia.Templates." + context.Request.ContentType))
                template = ConfigurationManager.AppSettings["WG2K.Hypermedia.Templates." + context.Request.ContentType];
            if (template != null)
            {
                return template;
            }
            return "unknown";
        }
    }
}