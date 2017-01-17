using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;
using System.Xml;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Publishing;
using Umbraco.Core.Security;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace wg2k.umbraco
{
    [Serializable]
    public class uHateoas : Dictionary<string, object>
    {
        private Dictionary<string, object> data { get; set; }
        private bool encodeHTML { get; set; }
        private HttpContext context { get; set; }
        private UmbracoContext uContext { get; set; }
        private IContentTypeService contentTypeService { get; set; }
        private IContentService contentService { get; set; }
        private IDataTypeService dataTypeService { get; set; }
        private UmbracoHelper umbHelper { get; set; }
        private IUser currentUser { get; set; }
        private List<object> entities { get; set; }
        private List<object> actions { get; set; }
        private List<IContentType> allowedContentTypes { get; set; }
        private IPublishedContent mainModel { get; set; }
        private int currentPageId { get; set; }
        private bool canCreate { get; set; }
        private bool canUpdate { get; set; }
        private bool canDelete { get; set; }
        private bool simpleJSON { get; set; }

        //Constructors
        public uHateoas()
        {
            Initialise();
        }
        public uHateoas(dynamic currentPage)
        {
            Initialise();
            foreach (var item in Process(currentPage))
            {
                this.Add(item.Key, item.Value);
            }
        }
        public uHateoas(dynamic currentPage, bool simple)
        {
            Initialise();
            foreach (var item in Process(currentPage, simple))
            {
                this.Add(item.Key, item.Value);
            }
        }
        public uHateoas(RenderModel model)
        {
            Initialise();
            foreach (var item in Process(model.Content))
            {
                this.Add(item.Key, item.Value);
            }
        }
        public uHateoas(RenderModel model, bool simple)
        {
            Initialise();
            foreach (var item in Process(model, simple))
            {
                this.Add(item.Key, item.Value);
            }
        }
        private void Initialise()
        {
            context = HttpContext.Current;
            string template = null;
            if (ConfigurationManager.AppSettings.AllKeys.Contains("WG2K.Hypermedia.Templates." + context.Request.ContentType))
                template = ConfigurationManager.AppSettings["WG2K.Hypermedia.Templates." + context.Request.ContentType];
            if (template != null)
                context.Response.ContentType = context.Request.ContentType;
            else
                context.Response.ContentType = "text/json";
            uContext = UmbracoContext.Current;
            umbHelper = new UmbracoHelper(uContext);
            canCreate = false;
            canUpdate = false;
            canDelete = false;
        }

        //Process Overrides
        public HtmlString Process()
        {
            return new HtmlString(JsonConvert.SerializeObject(this));
        }
        public HtmlString Process(string xmlRoot)
        {
            return new HtmlString(JsonConvert.DeserializeXmlNode(JsonConvert.SerializeObject(this), xmlRoot).OuterXml);
        }
        public Dictionary<string, object> Process(dynamic currentPage)
        {
            currentPageId = currentPage.Id;
            return Process(umbHelper.TypedContent(currentPageId), false);
        }
        public Dictionary<string, object> Process(dynamic currentPage, bool simple)
        {
            currentPageId = currentPage.Id;
            return Process(umbHelper.TypedContent(currentPageId), simple);
        }
        public Dictionary<string, object> Process(RenderModel model)
        {
            currentPageId = model.Content.Id;
            return Process(model.Content, false);
        }
        public Dictionary<string, object> Process(RenderModel model, bool simple)
        {
            currentPageId = model.Content.Id;
            return Process(model.Content, simple);
        }
        
        //Process Current Model
        public Dictionary<string, object> Process(IPublishedContent model, bool simple)
        {
            mainModel = model;
            currentPageId = model.Id;
            simpleJSON = simple;
            FormsAuthenticationTicket ticket = new HttpContextWrapper(HttpContext.Current).GetUmbracoAuthTicket();
            try
            {
                if (ticket != null && ticket.Expired != true)
                {
                    string userName = ticket.Name;
                    if (userName != null)
                    {
                        currentUser = ApplicationContext.Current.Services.UserService.GetByUsername(userName);
                        if (currentUser != null)
                        {
                            IEnumerable<string> permissions = currentUser.DefaultPermissions;
                            if (currentUser.UserType.Alias == "admin")
                            {
                                contentTypeService = ApplicationContext.Current.Services.ContentTypeService;
                                dataTypeService = ApplicationContext.Current.Services.DataTypeService;
                                IContentType currentContentType = contentTypeService.GetContentType(model.ContentType.Id);
                                if (currentContentType != null)
                                {
                                    canUpdate = true;
                                    canDelete = true;
                                    if (currentContentType.AllowedContentTypes.Count() > 0)
                                        canCreate = true;
                                }
                            }
                        }
                    }
                }
                if (context.Request.HttpMethod == "GET")
                {
                    if (context.Request["action"] != null && context.Request["doctype"] != null)
                    {
                        data = BuildForm(model, context.Request["doctype"].ToString());
                    }
                    else
                    {
                        if (context.Request.Url.Host.Contains("wg2k.com") || context.Request["nocache"] != null)
                        {
                            data = ProcessRequest(model);
                        }
                        else
                        {
                            if (context.Cache[context.Request.Url.PathAndQuery] == null)
                            {
                                context.Cache.Insert(context.Request.Url.PathAndQuery, ProcessRequest(model),
                                null, System.Web.Caching.Cache.NoAbsoluteExpiration,
                                new TimeSpan(0, 10, 0));
                            }
                            data = (Dictionary<string, object>)context.Cache[context.Request.Url.PathAndQuery];
                        }
                    }
                }
                else
                {
                    data = ProcessForm(model);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Process Error " + ex.Message);
            }
            return data;
        }

        //Process Helper Methods
        private Dictionary<string, object> ProcessRequest(IPublishedContent model)
        {
            entities = new List<object>();
            actions = new List<object>();
            try
            {
                encodeHTML = false;
                if (context.Request["currentmodel"] != null)
                {
                    model = umbHelper.TypedContent(context.Request["currentmodel"]);
                }
                if (context.Request["encodeHTML"] != null)
                {
                    encodeHTML = context.Request["encodeHTML"].AsBoolean();
                }
                if (context.Request["ancestor"] != null)
                {
                    IPublishedContent ancestor = model.AncestorOrSelf(context.Request["ancestor"].ToString());
                    return Simplify(ancestor);
                }
                entities.AddRange(GetDescendantEntities(model));
                entities.AddRange(GetChildrenEntities(model));
                if (!simpleJSON && (canCreate || canUpdate || canDelete))
                    actions.AddRange(GetChildrenActions(model));
                Dictionary<string, object> simpleNode = Simplify(model, true, entities, actions);
                return simpleNode;
            }
            catch (Exception ex)
            {
                throw new Exception("ProcessRequest Error " + ex.Message);
            }
        }
        private Dictionary<string, object> Simplify(IPublishedContent node)
        {
            return Simplify(node, false, new List<object>(), new List<object>());
        }
        private Dictionary<string, object> Simplify(IPublishedContent node, bool isRoot, List<object> entities, List<object> actions)
        {
            try
            {
                if (!HasAccess(node))
                    throw new Exception("Access Denied");
                Dictionary<string, object> Properties = new Dictionary<string, object>();
                PropertyInfo[] props = typeof(IPublishedContent).GetProperties();
                List<object> links = new List<object>();
                SortedDictionary<string, object> properties = new SortedDictionary<string, object>();
                foreach (PropertyInfo pi in props.OrderBy(p => p.Name))
                {
                    switch (pi.Name)
                    {
                        case "ContentSet":
                        case "ContentType":
                        case "PropertiesAsList":
                        case "ChildrenAsList":
                        case "Properties":
                        case "Item":
                        case "Version":
                            break;
                        case "Parent":
                            if (node.Parent != null)
                                if (HasAccess(node.Parent))
                                    links.Add(new { rel = new string[] { "_Parent", node.Parent.DocumentTypeAlias }, title = node.Parent.Name, href = GetHateoasHref(node.Parent, null) });
                            break;
                        case "Url":
                            links.Add(new { rel = new string[] { "_Self", node.DocumentTypeAlias }, title = node.Name, href = GetHateoasHref(node, null) });
                            properties.Add(pi.Name, node.Url);
                            break;
                        case "Children":
                        case "GetChildrenAsList":
                            string url = string.Empty;
                            List<IPublishedContent> linkList = node.Children.ToList();
                            foreach (IPublishedContent child in linkList)
                            {
                                if (HasAccess(child))
                                    links.Add(new { rel = new string[] { "_Child", child.DocumentTypeAlias }, title = child.Name, href = GetHateoasHref(child, null) });
                            }
                            break;
                        case "DocumentTypeAlias":
                        case "NodeTypeAlias":
                            SortedSet<string> classes = new SortedSet<string>();
                            classes.Add(node.DocumentTypeAlias);
                            if (context.Request["descendants"] != null && isRoot)
                            {
                                classes.Add("Descendants");
                            }
                            if (context.Request["children"] != null && isRoot)
                            {
                                classes.Add("Children");
                            }
                            if (simpleJSON)
                                Properties.Add("class", string.Join(",", classes.ToArray()));
                            else
                                Properties.Add("class", classes.ToArray());
                            Properties.Add("title", node.Name);
                            goto default;
                        default:
                            KeyValuePair<string, object> prop = SimplyfyProperty(pi, node);
                            properties.Add(prop.Key, prop.Value);
                            break;
                    }
                }
                foreach (IPublishedProperty pal in node.Properties)
                {
                    if (pal != null)
                    {
                        KeyValuePair<string, object> prop = SimplyfyProperty(pal, node);
                        properties.Add(prop.Key, prop.Value);
                    }
                }
                // resolve any nested content nodes specified by the resolveContent switch
                ResolveContent(properties);
                if (context.Request["select"] != null)
                {
                    SortedDictionary<string, object> selectedProperties = new SortedDictionary<string, object>();
                    properties.Where(p => (context.Request["select"].ToString().ToLower().Split(',').Contains(p.Key.ToLower()))).ForEach(a => selectedProperties.Add(a.Key, properties[a.Key]));
                    properties = selectedProperties;
                }
                Properties.Add("properties", properties);
                if (entities.Count > 0)
                {
                    Properties.Add("entities", entities);
                }
                if (actions.Count > 0 && simpleJSON == false)
                {
                    Properties.Add("actions", actions);
                }
                if (links.Count > 0 && simpleJSON == false)
                {
                    Properties.Add("links", links);
                }
                return Properties;
            }
            catch (Exception ex)
            {
                throw new Exception("Simplify Error " + ex.Message);
            }
        }
        private Dictionary<string, object> BuildForm(IPublishedContent model, string docTypeAlias)
        {
            actions = new List<object>();
            try
            {
                encodeHTML = false;
                if (canCreate || canUpdate || canDelete)
                    actions.AddRange(GetChildrenActions(model));
                else
                    return ProcessRequest(model);
                Dictionary<string, object> simpleNode = GenerateForm(model, docTypeAlias);
                if (actions.Count > 0)
                {
                    simpleNode.Add("actions", actions);
                }
                return simpleNode;
            }
            catch (Exception ex)
            {
                throw new Exception("BuildForm Error " + ex.Message);
            }
        }
        private Dictionary<string, object> ProcessForm(IPublishedContent model)
        {
            Dictionary<string, object> node = new Dictionary<string, object>();
            try
            {
                if (context.Request.QueryString["doctype"] == null)
                    throw new Exception("No doctype supplied");
                string doctype = context.Request.QueryString["doctype"].ToString();
                string action = context.Request.HttpMethod.ToUpper();
                bool delete = context.Request.QueryString["delete"] == null ? false : context.Request.QueryString["delete"].ToString().ToLower() == "true";
                bool publish = context.Request.QueryString["publish"] == null ? false : context.Request.QueryString["publish"].ToString().ToLower() == "true";
                contentService = ApplicationContext.Current.Services.ContentService;
                switch (action)
                {
                    case "POST":
                        if (!canCreate)
                            throw new Exception(action + " Access Denied");
                        node = ProcessRequest(CreateNode(model, doctype, publish));
                        break;
                    case "PUT":
                        if (!canUpdate)
                            throw new Exception(action + " Access Denied");
                        node = ProcessRequest(UpdateNode(model, doctype, publish));
                        break;
                    case "PATCH":
                        if (!canUpdate)
                            throw new Exception(action + " Access Denied");
                        node = ProcessRequest(UpdateNode(model, doctype, publish));
                        break;
                    case "DELETE":
                        if (!canDelete)
                            throw new Exception(action + " Access Denied");
                        node = ProcessRequest(RemoveNode(model, doctype, delete));
                        break;
                    default:
                        throw new Exception(action + " is an invalid action");
                }
            }
            catch (Exception ex)
            {
               throw new Exception("Process Form Error " + ex.Message);
            }
            return node;
        }
        private IPublishedContent RemoveNode(IPublishedContent model, string docType, bool delete)
        {
            IPublishedContent node;
            try
            {
                IContent deleteNode = contentService.GetById(model.Id);
                if (deleteNode == null)
                    throw new Exception("Node is null");
                if (delete)
                    contentService.Delete(deleteNode, currentUser.Id);
                else
                    contentService.UnPublish(deleteNode, currentUser.Id);
                node = umbHelper.TypedContent(deleteNode.ParentId);
            }
            catch (Exception ex)
            {
                throw new Exception("RemoveNode Error " + ex.Message);
            }
            return node;
        }
        private IPublishedContent UpdateNode(IPublishedContent model, string docType, bool publish)
        {
            IPublishedContent node ;
            try
            {
                string json = GetPostedJSON();
                Dictionary<string, object> form = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                IContent updateNode = contentService.GetById(model.Id);
                if (updateNode == null)
                    throw new Exception("Node is null");
                if (form.ContainsKey("Name"))
                    updateNode.Name = form["Name"].ToString();
                if (form.ContainsKey("ExpireDate"))
                    updateNode.ExpireDate = (DateTime)form["ExpireDate"];
                if (form.ContainsKey("ReleaseDate"))
                    updateNode.ReleaseDate = (DateTime)form["ReleaseDate"];
                foreach (Property prop in updateNode.Properties)
                {
                    try
                    {
                        KeyValuePair<string, object> kvp = GetValuePair(prop.Alias, form, updateNode);
                        if (kvp.Value != null)
                            updateNode.SetValue(kvp.Key, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Debug<uHateoas>("Node property error: \"{0}\"", () => ex.Message);
                    }
                }
                if (publish)
                {
                    Attempt<PublishStatus> result = contentService.SaveAndPublishWithStatus(updateNode, currentUser.Id);
                    node = umbHelper.TypedContent(result.Result.ContentItem.Id);
                }
                else
                {
                    contentService.Save(updateNode, currentUser.Id);
                    node =umbHelper.TypedContent(updateNode.Id);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("UpdateNode Error " + ex.Message);
            }
            return node;
        }
        private IPublishedContent CreateNode(IPublishedContent model, string docType, bool publish)
        {
            IPublishedContent node;
            try
            {
                string json = GetPostedJSON();
                Dictionary<string, object> form = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (form == null || (form != null && form.ContainsKey("Name") == false))
                    throw new Exception("Name form element is required");
                IContent parentNode = contentService.GetById(model.Id);
                IContent newNode = contentService.CreateContent(form["Name"].ToString(), parentNode, docType, currentUser.Id);
                if (newNode == null)
                    throw new Exception("New Node is null");
                if (form.ContainsKey("ExpireDate"))
                    newNode.ExpireDate = (DateTime)form["ExpireDate"];
                if (form.ContainsKey("ReleaseDate"))
                    newNode.ReleaseDate = (DateTime)form["ReleaseDate"];
                foreach (Property prop in newNode.Properties)
                {
                    try
                    {
                        KeyValuePair<string, object> kvp = GetValuePair(prop.Alias, form, newNode);
                        if (kvp.Value != null)
                            newNode.SetValue(kvp.Key, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Debug<uHateoas>("New node property error: \"{0}\"", () => ex.Message);
                    }
                }
                if (publish)
                {
                    Attempt<PublishStatus> result = contentService.SaveAndPublishWithStatus(newNode, currentUser.Id);
                    node = umbHelper.TypedContent(result.Result.ContentItem.Id);
                }
                else
                {
                    contentService.Save(newNode, currentUser.Id);
                    node = model;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("CreateNode Error " + ex.Message);
            }
            return node;
        }
        private Dictionary<string, object> GenerateForm(IPublishedContent node, string docTypeAlias)
        {
            try
            {
                Dictionary<string, object> Properties = new Dictionary<string, object>();
                List<object> links = new List<object>();
                SortedDictionary<string, object> properties = new SortedDictionary<string, object>();
                SortedSet<string> classes = new SortedSet<string>();
                string title = "";
                IContentType doc = contentTypeService.GetContentType(docTypeAlias);
                if (context.Request["action"].ToString() == "create")
                {
                    if (doc != null)
                    {
                        docTypeAlias = doc.Alias;
                        title = "New " + docTypeAlias;
                        AddContentTypeProperties(doc, properties, dataTypeService, null);
                        while (doc.ParentId != -1)
                        {
                            doc = contentTypeService.GetContentType(doc.ParentId);
                            AddContentTypeProperties(doc, properties, dataTypeService, null);
                        }
                        properties.Add("Name", new { description = "Name for the Node", group = "Properties", manditory = true, propertyEditor = "Umbraco.Textbox", title = "Name", type = "text", validation = "([^\\s]*)", value = "" });
                        properties.Add("ExpiryDate", new { description = "Date for the Node to be Unpublished", group = "Properties", manditory = false, propertyEditor = "date", title = "Expriry Date", type = "text", validation = "", value = "" });
                        properties.Add("ReleaseDate", new { description = "Date for the Node to be Published", group = "Properties", manditory = false, propertyEditor = "date", title = "Release Date", type = "text", validation = "", value = "" });

                    }
                }
                if (context.Request["action"].ToString() == "update")
                {
                    if (doc != null)
                    {
                        docTypeAlias = doc.Alias;
                        title = "Update " + docTypeAlias;
                        AddContentTypeProperties(doc, properties, dataTypeService, node);
                        while (doc.ParentId != -1)
                        {
                            doc = contentTypeService.GetContentType(doc.ParentId);
                            AddContentTypeProperties(doc, properties, dataTypeService, node);
                        }
                        properties.Add("Name", new { description = "Name for the Node", group = "Properties", manditory = true, propertyEditor = "Umbraco.Textbox", title = "Name", type = "text", validation = "([^\\s]*)", value = node.Name });
                        properties.Add("ExpiryDate", new { description = "Date for the Node to be Unpublished", group = "Properties", manditory = false, propertyEditor = "date", title = "Expiry Date", type = "text", validation = "", value= "" });
                        properties.Add("ReleaseDate", new { description = "Date for the Node to be Published", group = "Properties", manditory = false, propertyEditor = "date", title = "Release Date", type = "text", validation = "", value = "" });

                    }
                }
                if (context.Request["action"].ToString() == "remove")
                {
                    if (doc != null)
                    {
                        docTypeAlias = doc.Alias;
                        title = "Update " + docTypeAlias;
                        AddContentTypeProperties(doc, properties, dataTypeService, node);
                        while (doc.ParentId != -1)
                        {
                            doc = contentTypeService.GetContentType(doc.ParentId);
                            AddContentTypeProperties(doc, properties, dataTypeService, node);
                        }
                        properties.Add("Name", new { description = "Name for the Node", group = "Properties", manditory = true, propertyEditor = "Umbraco.Textbox", title = "Name", type = "text", validation = "([^\\s]*)", value = node.Name });
                    }
                }
                classes.Add("x-form");
                classes.Add(docTypeAlias);
                Properties.Add("class", classes.ToArray());
                Properties.Add("title", title);
                Properties.Add("properties", properties);
                return Properties;
            }
            catch (Exception ex)
            {
                throw new Exception("GenerateForm Error " + ex.Message);
            }
        }        
        private KeyValuePair<string, object> SimplyfyProperty(PropertyInfo prop, IPublishedContent node)
        {
            object val = new Object();
            try
            {
                val = prop.GetValue(node, null);
                val = ResolveMedia(prop.Name, val);
                if (context.Request["html"] != null && context.Request["html"].ToString().ToLower() == "false")
                    if (val.GetType() == typeof(System.String))
                    {
                        val = val.ToString().StripHtml();
                    }
            }
            catch (Exception ex)
            {
                val = ex.Message;
            }
            string propTitle = Regex.Replace(prop.Name, "(\\B[A-Z])", " $1");
            if (simpleJSON)
                return new KeyValuePair<string, object>(prop.Name, val);
            return new KeyValuePair<string, object>(prop.Name, new { title = propTitle, value = val, type = GetPropType(val, "text") });
        }
        private KeyValuePair<string, object> SimplyfyProperty(IPublishedProperty prop, IPublishedContent node)
        {
            object val = prop.Value;
            PublishedPropertyType pubPropType = node.ContentType.GetPropertyType(prop.PropertyTypeAlias);
            string PropertyEditorAlias = pubPropType.PropertyEditorAlias;
            string propName = prop.PropertyTypeAlias; // prop.PropertyTypeAlias.Substring(0, 1).ToUpper() + prop.PropertyTypeAlias.Substring(1);
            string propTitle = Regex.Replace(propName, "(\\B[A-Z])", " $1");
            if (val != null)
            {
                if (val.ToString().Contains("/>") || val.ToString().Contains("</"))
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(val.ToString());
                        val = JsonConvert.SerializeXmlNode(doc);
                    }
                    catch (Exception)
                    {
                        if (encodeHTML)
                            val = context.Server.HtmlEncode(val.ToString());
                    }
                }
                val = ResolveMedia(propName, val).ToString();
                if (context.Request["html"] != null && context.Request["html"].ToString().ToLower() == "false")
                    val = val.ToString().StripHtml();
            }
            if (simpleJSON)
                return new KeyValuePair<string, object>(prop.PropertyTypeAlias.ToFirstUpper(), SetPropType(val, GetPropType(val, PropertyEditorAlias)));
            return new KeyValuePair<string, object>(prop.PropertyTypeAlias, new { title = propTitle, value = SetPropType(val, GetPropType(val, PropertyEditorAlias)), type = GetPropType(val, PropertyEditorAlias), propertyEditor = PropertyEditorAlias });
        }
        private KeyValuePair<string, object> GetValuePair(string alias, Dictionary<string, object> form, IContent newNode)
        {
            KeyValuePair<string, object> val = new KeyValuePair<string, object>(alias, null);
            PropertyType propType = newNode.PropertyTypes.FirstOrDefault(p => p.Alias == alias);
            if (propType == null)
                return val;
            if (form[alias] == null)
                return val;
            IDataTypeDefinition dtd = dataTypeService.GetDataTypeDefinitionById(propType.DataTypeDefinitionId);
            switch (dtd.DatabaseType)
            {
                case DataTypeDatabaseType.Date:
                    val = new KeyValuePair<string, object>(alias, DateTime.Parse(form[alias].ToString()));
                    break;
                case DataTypeDatabaseType.Integer:
                    val = new KeyValuePair<string, object>(alias, int.Parse(form[alias].ToString()));
                    break;
                default:
                    val = new KeyValuePair<string, object>(alias, form[alias].ToString());
                    break;
            }
            return val;
        }
        private List<object> GetChildrenActions(IPublishedContent node)
        {
            Dictionary<string, object> action = new Dictionary<string, object>();
            List<object> actions = new List<object>();
            SortedSet<string> classes = new SortedSet<string>();
            if (canCreate || canUpdate || canDelete)
            {
                BuildGetActions(node, ref action, actions, ref classes);
                BuildPostAction(node, ref action, actions, ref classes);
            }
            return actions;
        }
        private List<object> GetOptions(IPublishedContent model)
        {
            throw new NotImplementedException();
        }
        private List<object> ProcessTakeSkip(List<object> entities)
        {
            if (context.Request["skip"] != null && context.Request["skip"].ToString().IsNumeric() && context.Request["take"] != null && context.Request["take"].ToString().IsNumeric())
            {
                return entities.Skip(context.Request["skip"].ToString().AsInteger()).Take(context.Request["take"].ToString().AsInteger()).ToList();
            }
            if (context.Request["take"] != null && context.Request["take"].ToString().IsNumeric())
            {
                return entities.Take(context.Request["take"].ToString().AsInteger()).ToList();
            }
            if (context.Request["skip"] != null && context.Request["skip"].ToString().IsNumeric())
            {
                return entities.Skip(context.Request["skip"].ToString().AsInteger()).ToList();
            }
            return entities;
        }
        private List<object> GetDescendantEntities(IPublishedContent model)
        {
            List<object> entities = new List<object>();
            if (context.Request["descendants"] != null)
            {
                IEnumerable<IPublishedContent> descendants;
                string descendantsAlias = context.Request["descendants"];
                if (descendantsAlias == "")
                    descendants = model.Descendants();
                else if (descendantsAlias.IsNumeric())
                    descendants = model.Descendants(int.Parse(descendantsAlias));
                else if (descendantsAlias.Contains(","))
                {
                    List<IPublishedContent> listDescendants = new List<IPublishedContent>();
                    string[] descendantAliasArray = descendantsAlias.Split(',');
                    foreach (string descendantAlias in descendantAliasArray)
                    {
                        listDescendants.AddRange(model.Descendants(descendantAlias));
                    }
                    descendants = listDescendants;
                }
                else
                    descendants = model.Descendants(descendantsAlias);
                List<IPublishedContent> descendantList = new List<IPublishedContent>();
                if (context.Request["where"] != null)
                    descendantList = descendants.Where(context.Request["where"].ToString().Replace(" eq ", " = ").Replace(" ge ", " >= ").Replace(" gt ", " > ").Replace(" le ", " <= ").Replace(" lt ", " < ").Replace(" ne ", " != ").Replace("'", "\"").Replace(" and ", " && ").Replace(" or ", " || ")).OrderBy(x => x.SortOrder).ToList();
                else
                {
                    foreach (IPublishedContent descendant in descendants.OrderBy(x => x.SortOrder))
                    {
                        descendantList.Add(descendant);
                    }
                }
                List<object> descendantObjectList = new List<object>();
                foreach (IPublishedContent d in descendantList)
                {
                    descendantObjectList.Add(Simplify(d));
                }
                entities.AddRange(descendantObjectList);
            }
            return ProcessTakeSkip(entities);
        }
        private List<object> GetChildrenEntities(IPublishedContent CurrentModel)
        {
            List<object> entities = new List<object>();
            if (context.Request["children"] != null)
            {
                IEnumerable<IPublishedContent> children = CurrentModel.Children;
                List<IPublishedContent> childList = new List<IPublishedContent>();
                if (context.Request["where"] != null)
                    childList = children.Where(context.Request["where"].ToString().Replace(" eq ", " = ").Replace(" ge ", " >= ").Replace(" gt ", " > ").Replace(" le ", " <= ").Replace(" lt ", " < ").Replace(" ne ", " != ").Replace("'", "\"").Replace(" and ", " && ").Replace(" or ", " || ")).OrderBy(x => x.SortOrder).ToList();
                else
                {
                    foreach (IPublishedContent child in children.OrderBy(x => x.SortOrder))
                    {
                        childList.Add(child);
                    }
                }
                List<object> childObjectList = new List<object>();
                foreach (IPublishedContent child in childList)
                {
                    childObjectList.Add(Simplify(child));
                }
                entities.AddRange(childObjectList);
            }
            return ProcessTakeSkip(entities);
        }
        private object ResolveMedia(string name, object property)
        {
            if (context.Request["resolveMedia"] != null)
            {
                foreach (string key in context.Request["resolveMedia"].ToString().Split(','))
                {
                    if (name.ToLower() == key.ToLower())
                    {
                        string url = string.Empty;
                        try
                        {
                            if (property.ToString().Contains(","))
                            {
                                List<string> resolvedMediaItems = new List<string>();
                                foreach (string subKey in property.ToString().Split(','))
                                {
                                    url = umbHelper.TypedMedia(subKey).Url;
                                    resolvedMediaItems.Add(url);
                                }
                                property = string.Join(",", resolvedMediaItems.ToArray());
                            }
                            else
                            {
                                url = umbHelper.TypedMedia(property.ToString()).Url;
                            }
                        }
                        catch (Exception)
                        {
                            url = "#";
                        }
                        property = url;
                    }
                }
            }
            return property;
        }
        private object SetPropType(object val, string guessType)
        {
            switch (guessType)
            {
                case "number":
                    return int.Parse(val.ToString());
                case "checkbox":
                    return val.ToString() == "True";
                default:
                    return val;
            }
        }
        private string GetPostedJSON()
        {
            StreamReader reader = new StreamReader(context.Request.InputStream);
            return reader.ReadToEnd();
        }
        private string GetPropType(object val, string propType)
        {
            int iout = 0;
            if (val == null)
                return "text";
            if (val.GetType() == typeof(System.String))
            {
                if (val.ToString() == "True" || val.ToString() == "False")
                    return "checkbox";
                if (val.ToString().Contains("-") && val.ToString().Contains("T") && val.ToString().Contains(":"))
                    return "date";
                if (val.ToString().IsNumeric() && int.TryParse(val.ToString(), out iout))
                    return "number";
            }
            if (val.GetType() == typeof(System.Int32))
            {
                return "number";
            }
            if (val.GetType() == typeof(System.Boolean))
            {
                return "checkbox";
            }
            if (val.GetType() == typeof(System.DateTime))
            {
                return "date";
            }
            return "text";
        }
        private string GetHateoasHref(IPublishedContent node, object queryString)
        {
            string[] segments = context.Request.Url.Segments;
            string lastSegment = segments.LastOrDefault();
            string template = "";
            if (lastSegment != null && lastSegment != "/" && lastSegment != mainModel.UrlName && lastSegment != mainModel.UrlName + "/")
                template = lastSegment;
            string href = string.Format("{0}{1}{2}{3}", context.Request.Url.Scheme + "://", context.Request.Url.Host, node.Url, template);
            if (queryString != null)
            {
                PropertyInfo[] nvps = queryString.GetType().GetProperties();
                foreach (PropertyInfo nvp in nvps)
                {
                    href += (href.Contains("?") ? "&" : "?") + nvp.Name + "=" + nvp.GetValue(queryString, null).ToString();
                }
            }
            return href;
        }
        private bool HasAccess(IPublishedContent node)
        {
            if (umbHelper.IsProtected(node.Id, node.Path))
                if (umbHelper.MemberHasAccess(node.Id, node.Path))
                    return true;
                else
                    return false;
            return true;
        }
        private static string GetSimpleType(IDataTypeDefinition dtd)
        {
            string val = "text";
            switch (dtd.DatabaseType)
            {
                case DataTypeDatabaseType.Date:
                    val = "date";
                    break;
                case DataTypeDatabaseType.Integer:
                    val = "number";
                    break;
                default:
                    val = "text";
                    break;
            }
            return val;
        }
        private static void AddContentTypeProperties(IContentType newDoc, SortedDictionary<string, object> properties, IDataTypeService dataTypeService, IPublishedContent node)
        {
            if (newDoc != null && newDoc.PropertyGroups != null)
            {
                foreach (PropertyGroup propGroup in newDoc.PropertyGroups)
                {
                    foreach (PropertyType propType in propGroup.PropertyTypes)
                    {
                        if (!properties.ContainsKey(propType.Alias))
                        {
                            IDataTypeDefinition dtd = dataTypeService.GetDataTypeDefinitionById(propType.DataTypeDefinitionId);
                            SortedDictionary<string, object> property = new SortedDictionary<string, object>();
                            property.Add("title", propType.Name);
                            property.Add("value", node == null ? "" : node.GetPropertyValue<string>(propType.Alias));
                            property.Add("group", propGroup.Name);
                            property.Add("type", GetSimpleType(dtd));
                            property.Add("manditory", propType.Mandatory);
                            property.Add("validation", propType.ValidationRegExp);
                            property.Add("description", propType.Description);
                            property.Add("propertyEditor", propType.PropertyEditorAlias);
                            IEnumerable<string> prevalues = dataTypeService.GetPreValuesByDataTypeId(propType.DataTypeDefinitionId);
                            if (prevalues != null && prevalues.Count() > 0)
                            {
                                property.Add("prevalues", prevalues);
                            }
                            properties.Add(propType.Alias, property);
                        }
                    }
                }
            }
        }
        private void BuildGetActions(IPublishedContent node, ref Dictionary<string, object> action, List<object> actions, ref SortedSet<string> classes)
        {
            if (!context.Request.Params.AllKeys.Contains("action"))
            {
                IContentType currentContentType = contentTypeService.GetContentType(node.ContentType.Id);
                if (currentContentType != null && currentContentType.AllowedContentTypes.Count() > 0 && canCreate)
                {
                    allowedContentTypes = contentTypeService.GetAllContentTypes(currentContentType.AllowedContentTypes.Select(ct => ct.Id.Value).ToArray()).ToList();
                    if (allowedContentTypes != null && allowedContentTypes.Count() > 0)
                    {
                        foreach (IContentType ct in allowedContentTypes)
                        {
                            action = new Dictionary<string, object>();
                            classes = new SortedSet<string>();
                            classes.Add(ct.Alias);
                            //create
                            classes.Add("x-form");
                            action.Add("class", classes.ToArray());
                            action.Add("title", "Create @content".SmartReplace(new { content = ct.Name }));
                            action.Add("method", "GET");
                            action.Add("href", GetHateoasHref(node, new { action = "create", doctype = ct.Alias, publish = "false" }));
                            action.Add("type", context.Request.ContentType);
                            actions.Add(action);
                        }
                    }
                }
                if (canUpdate || canDelete)
                {
                    classes = new SortedSet<string>();
                    classes.Add(currentContentType.Alias);
                    classes.Add("x-form");
                }
                if (canUpdate)
                {
                    //update
                    action = new Dictionary<string, object>();
                    action.Add("class", classes.ToArray());
                    action.Add("title", "Update @content".SmartReplace(new { content = currentContentType.Name }));
                    action.Add("method", "GET");
                    action.Add("href", GetHateoasHref(node, new { action = "update", doctype = currentContentType.Alias, publish = "false" }));
                    action.Add("type", context.Request.ContentType);
                    actions.Add(action);
                }
                if (canDelete)
                {
                    //delete
                    action = new Dictionary<string, object>();
                    action.Add("class", classes.ToArray());
                    action.Add("method", "GET");
                    action.Add("title", "Remove @content".SmartReplace(new { content = currentContentType.Name }));
                    action.Add("href", GetHateoasHref(node, new { action = "remove", doctype = currentContentType.Alias, delete = "false" }));
                    action.Add("type", context.Request.ContentType);
                    actions.Add(action);
                }
            }
        }
        private void BuildPostAction(IPublishedContent node, ref Dictionary<string, object> action, List<object> actions, ref SortedSet<string> classes)
        {
            if (context.Request["action"] != null && context.Request["action"].ToString() == "create" && context.Request["doctype"] != null)
            {
                IContentType currentContentType = contentTypeService.GetContentType(node.ContentType.Id);
                if (currentContentType != null && currentContentType.AllowedContentTypes.Count() > 0)
                {
                    allowedContentTypes = contentTypeService.GetAllContentTypes(currentContentType.AllowedContentTypes.Select(ct => ct.Id.Value).ToArray()).ToList();
                    if (allowedContentTypes != null && allowedContentTypes.Count() > 0)
                    {
                        foreach (IContentType ct in allowedContentTypes)
                        {
                            action = new Dictionary<string, object>();
                            if (ct.Alias == context.Request["doctype"].ToString())
                            {
                                classes = new SortedSet<string>();
                                classes.Add(ct.Alias);
                                classes.Add("x-form");
                                action.Add("class", classes.ToArray());
                                action.Add("title", "Save @content".SmartReplace(new { content = ct.Name }));
                                action.Add("method", "POST");
                                action.Add("action", GetHateoasHref(node, new { doctype = ct.Alias, publish = "true" }));
                                action.Add("type", context.Request.ContentType);
                                actions.Add(action);
                            }
                        }
                    }
                }
            }
            if (context.Request["action"] != null && context.Request["action"].ToString() == "update")
            {
                IContentType ct = contentTypeService.GetContentType(node.ContentType.Id);
                action = new Dictionary<string, object>();
                if (ct.Alias == context.Request["doctype"].ToString())
                {
                    classes = new SortedSet<string>();
                    classes.Add(ct.Alias);
                    classes.Add("x-form");
                    action.Add("class", classes.ToArray());
                    action.Add("title", "Update @content".SmartReplace(new { content = ct.Name }));
                    action.Add("method", "PUT");
                    action.Add("action", GetHateoasHref(node, new { doctype = ct.Alias, publish = "true" }));
                    action.Add("type", context.Request.ContentType);
                    actions.Add(action);
                }
            }
            if (context.Request["action"] != null && context.Request["action"].ToString() == "remove")
            {
                IContentType ct = contentTypeService.GetContentType(node.ContentType.Id);
                action = new Dictionary<string, object>();
                if (ct.Alias == context.Request["doctype"].ToString())
                {
                    classes = new SortedSet<string>();
                    classes.Add(ct.Alias);
                    classes.Add("x-form");
                    action.Add("class", classes.ToArray());
                    action.Add("title", "Remove @content".SmartReplace(new { content = ct.Name }));
                    action.Add("method", "DELETE");
                    action.Add("action", GetHateoasHref(node, new { doctype = ct.Alias, delete = "false" }));
                    action.Add("type", context.Request.ContentType);
                    actions.Add(action);
                }
            }
            if (context.Request["action"] != null)
            {
                action = new Dictionary<string, object>();
                classes = new SortedSet<string>();
                classes.Add(node.DocumentTypeAlias);
                action.Add("class", classes.ToArray());
                action.Add("title", "Cancel");
                action.Add("method", "GET");
                action.Add("action", GetHateoasHref(node, null));
                action.Add("type", context.Request.ContentType);
                actions.Add(action);
            }

        }
        private void ResolveContent(SortedDictionary<string, object> properties)
        {
            if (context.Request["resolveContent"] != null)
            {
                foreach (string key in context.Request["resolveContent"].ToString().Split(','))
                {
                    if (properties.ContainsKey(key))
                    {
                        try
                        {
                            if (((dynamic)(properties[key])).GetType() == typeof(Int32))
                            {
                                int nodeid = (Int32) properties[key];
                                if (nodeid != currentPageId && key != "Path")
                                    properties[key] = Simplify(umbHelper.TypedContent(nodeid));
                            }
                            else if ((dynamic)(properties[key]).GetType() == typeof(string))
                            {
                                List<object> content = new List<object>();
                                foreach (string node in ((string)(properties[key])).Split(','))
                                {
                                    int nodeid = int.Parse(node);
                                    if (nodeid != currentPageId && key != "Path")
                                        content.Add(Simplify(umbHelper.TypedContent(nodeid)));
                                }
                                properties[key] = content;
                            }
                        }
                        catch (Exception ex)
                        {
                            properties[key] = properties[key];
                        }
                    }
                }
            }
        }
    }
}