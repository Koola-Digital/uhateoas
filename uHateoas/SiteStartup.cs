using System;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Routing;

namespace wg2k.umbraco
{
    public class SiteStartup : ApplicationEventHandler
    {
        protected override void ApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarting(umbracoApplication, applicationContext);
            bool hypermediaTemplates = false;
            if (CheckAppSettings())
            {
                hypermediaTemplates = ConfigurationManager.AppSettings["WG2K.Hypermedia.Templates.enabled"] == "true";
                if (hypermediaTemplates)
                {
                    ContentFinderResolver.Current.InsertTypeBefore<ContentFinderByNiceUrl, ContentFinderByNiceUrlWithContentAccept>();
                    ContentFinderResolver.Current.RemoveType<ContentFinderByNiceUrl>();
                }
            }
        }
        private bool CheckAppSettings()
        {
            try
            {
                if (ConfigurationManager.AppSettings.AllKeys.Contains("WG2K.Hypermedia.Templates.enabled"))
                    return true;
                Configuration webConfigApp = WebConfigurationManager.OpenWebConfiguration("~");
                bool changes = false;
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains("WG2K.Hypermedia.Templates.enabled"))
                {
                    webConfigApp.AppSettings.Settings.Add("WG2K.Hypermedia.Templates.enabled", "true");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains("WG2K.Hypermedia.Templates.text/umbraco+json"))
                {
                    webConfigApp.AppSettings.Settings.Add("WG2K.Hypermedia.Templates.text/umbraco+json", "uhateoas");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains("WG2K.Hypermedia.Templates.text/json"))
                {
                    webConfigApp.AppSettings.Settings.Add("WG2K.Hypermedia.Templates.text/json", "ujson");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains("WG2K.Hypermedia.Templates.text/xml"))
                {
                    webConfigApp.AppSettings.Settings.Add("WG2K.Hypermedia.Templates.text/xml", "uxml");
                    changes = true;
                }
                if (changes)
                    webConfigApp.Save();
            }
            catch (Exception ex)
            {
                LogHelper.Debug<uHateoas>("CheckAppSettings error: \"{0}\"", () => ex.Message);
                return false;
            }
            return true;
        }
    }
}