using System;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using uHateoas.League;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;

namespace UHateoas
{
    public class SiteStartup : ApplicationEventHandler
    {
        protected override void ApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarting(umbracoApplication, applicationContext);

            ContentService.Published += ContentServicePublishEvent;
            ContentService.UnPublished += ContentServicePublishEvent;
            ContentService.Deleted += ContentServiceOnDeleted;

            if (CheckAppSettings())
            {
                var hypermediaTemplates = ConfigurationManager.AppSettings[$"{UExtensions.AppSettingsPrefix}.Templates.enabled"] == "true";
                if (hypermediaTemplates)
                {
                    ContentFinderResolver.Current.InsertTypeBefore<ContentFinderByNiceUrl, ContentFinderByNiceUrlWithContentAccept>();
                    ContentFinderResolver.Current.RemoveType<ContentFinderByNiceUrl>();
                }
            }
        }

        private void EmptyCache(string alias)
        {
            LogHelper.Info(GetType(), "Emptying uHateoas cache");
            if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.CacheDocTypes"))
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheByKeySearch(UExtensions.CachePrefix + alias + "-");
            }
            else
            {
                ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheByKeySearch(UExtensions.CachePrefix);
            }
        }

        private void ContentServiceOnDeleted(IContentService sender, DeleteEventArgs<IContent> e)
        {
            foreach (var item in e.DeletedEntities)
            {
                EmptyCache(item.ContentType.Alias);
            }
        }

        private void ContentServicePublishEvent(IPublishingStrategy sender, PublishEventArgs<IContent> args)
        {
            foreach (var item in args.PublishedEntities)
            {
                EmptyCache(item.ContentType.Alias);
            }
        }

        private static bool CheckAppSettings()
        {
            try
            {
                if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.enabled"))
                    return true;

                var changes = false;
                var webConfigApp = WebConfigurationManager.OpenWebConfiguration("~");

                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.enabled"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.enabled", "true");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.text/umbraco+json"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.text/umbraco+json", "uhateoas");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.text/json"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.text/json", "ujson");
                    changes = true;
                }
                if (!webConfigApp.AppSettings.Settings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.Templates.text/xml"))
                {
                    webConfigApp.AppSettings.Settings.Add($"{UExtensions.AppSettingsPrefix}.Templates.text/xml", "uxml");
                    changes = true;
                }

                if (changes)
                    webConfigApp.Save();
            }
            catch (Exception ex)
            {
                LogHelper.Error<uHateoas.League.UHateoas>($"UHateoas CheckAppSettings Error: \"{ex.Message}\"", ex);
                return false;
            }

            return true;
        }
    }
}