#What is HATEOAS ?
**HATEOAS** stands for **H**ypermedia **A**s **T**he **E**ngine **O**f **A**pplication **S**tate. It is a constraint of the **REST** application architecture that distinguishes it from most other network application architectures. The principle is that a client interacts with a network application entirely through hypermedia provided dynamically by application servers. 

A **REST** client needs no prior knowledge about how to interact with any particular application or server beyond a generic understanding of hypermedia. By contrast, in a service-oriented architecture (SOA), clients and servers interact through a fixed interface shared through documentation or an interface description language (IDL). The HATEOAS constraint decouples client and server in a way that allows the server functionality to evolve independently.


#What is **UHATEOAS** and why should we care ?

**UHATEOAS** is **HATEOAS** for **Umbraco**

Check out the uHateoas Project Site: http://uhateoas.league.co.za

We all love and use Umbraco to build our websites, web applications and mobile applications.

It provides us with a easy web based interface to define, capture and maintain content, entities, relationships, rules and attributes in an intuitive non technical interface.

When it comes to taking that content and using it on websites, mobile hybrid apps or anywhere you can dream of however, it assumes a certain level of understanding of the ASP.NET stack. Webforms, MVC, Razor, Web API etc.

This is not a problem for any ASP.NET developer, but what about someone that is great at HTML, CSS and Javascript and doesn't know or care about ASP.NET?

At the moment you would need a ASP.NET developer to expose selected functionality and data via a custom written Web API, that would expose certain parts of the data stored in Umbraco.

This could then be consumed by the front-end developer via the custom REST based API that the ASP.NET developer produced.

With UHATEOAS, this is not necessary.

Simply install the UHATEOAS package and your Umbraco content is now automatically discoverable, navigable, query-able, page-able and editable via a standard Hypermedia API that is driven by the Document Types, DataTypes, Structure, Rules and User / Member Access Control that you define through the Umbraco Back-Office.

#uHateoas API
The uHateoas API is designed to give RESTful clients access to the data stored as IPublishedContent in a typical Umbraco web site via the Content Service in a number of supported data formats i.e. json, xml, umbraco+json or any of the HATEOAS hypermedia formats ( hal, collection-json, siren etc... ) that you can implement via a simple Umbraco alternate template. The HATEOAS formats, specifically umbraco+json makes your entire Umbraco Content tree discoverable and navigable via the json link collections. The links are filtered based on membership access and only expose Published Content.

Three alternate templates are supplied with the package as a starting point, feel free to add your own and contribute back to the project:

**umbraco+json** - An umbraco centric Hypermedia format based on Siren

**json** - A basic json format without the Hypermedia Links and Actions

**xml** - An xml representation of the json format.

By default these three templates are associated with Content-Type headers that are configurable via the web.config and are integrated into the Umbraco Request Pipeline, so that if you supply the appropriate Content-Type header, the resource that you are asking for will be returned in that format without specifying the alternate template. For most developers, this would be the natural way of requesting the data.

###Content-Type templates ( web.config )
```xml   
<appSettings>
  <add key="WG2K.Hypermedia.Templates.enabled" value="true" />
  <add key="WG2K.Hypermedia.Templates.text/umbraco+json" value="uhateoas" />
  <add key="WG2K.Hypermedia.Templates.text/json" value="ujson" />
  <add key="WG2K.Hypermedia.Templates.text/xml" value="uxml" />
</appSettings>
``` 
```
Adding additional views/templates can be done simply by adding a new setting to appSettings for example : 
<add key="WG2K.Hypermedia.Templates.text/rss+xml" value="urss" /> 
```
###The uhateoas.cshtml view/template
```c#
/*
The simplest of all the templates
Instantiate an instance of uHateoas and call Process
*/
@inherits Umbraco.Web.Mvc.UmbracoTemplatePage
@using wg2k.umbraco
@((new uHateoas(Model)).Process())
```

###The ujson.cshtml view/template
```c#
/*
This example adds an additional boolean parameter to tell the API to output it as simple JSON
*/
@inherits Umbraco.Web.Mvc.UmbracoTemplatePage
@using wg2k.umbraco
@(new uHateoas(Model, true).Process())
``` 

###The uxml.cshtml view/template
```c#
/*
For xml we simply need to add the name of the "root" node for the xml doc as an optional parameter to the Process method.
*/
@inherits Umbraco.Web.Mvc.UmbracoTemplatePage
@using wg2k.umbraco
@((new uHateoas(Model, true)).Process("root"))
```

#General Usage

As mentioned if you specify a Content-Type such as **text/umbraco+json** or **text/json** or **text/xml**, then all you need for your url is http://www.mysite.com/anypath/anyresource

###text/html ( no surprises here !)
```http
http://www.mysite.com/anypath/anyresource
```
(no header specified, defaults to Content-Type : **text/html**) this is your typical website url, and as you would expect it simply outputs your webpage as html     

```html
<!DOCTYPE html>
<html>
  <head>
    <meta charset="utf-8" />
    <title>Slick - A one page HTML template for coding teams
    </title>
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <!-- Le Grand CSS -->
    <link href="css/bootstrap.min.css" rel="stylesheet" media="screen">
    <link href="css/font-awesome.min.css" rel="stylesheet" media="screen">
    <link href="css/bootstrap-extend.css" rel="stylesheet" media="screen">
    <link href="css/flexslider.css" rel="stylesheet" media="screen">
    <link href="css/style.css" rel="stylesheet" media="screen">
    <link rel="stylesheet" href="/lib/codemirror.css">
    <link rel="stylesheet" href="/addon/fold/foldgutter.css" />
    <link rel="stylesheet" href="/css/bootstrap-multiselect.css" />
 <!-- truncated for brevity -->
 ```

###text/umbraco+json
```http
http://www.mysite.com/anypath/anyresource 
```
(Content-Type : **text/umbraco+json**) the current resource is output as an umbraco+json hypermedia formatted document. 

```json   
{
  "class":[
    "umbHomePage"],
  "title":"uHateoas",
  "properties":{
    "bannerHeader":{
      "title":"Banner Header",
      "value":"What is HATEOAS ?",
      "type":"text",
      "propertyEditor":"Umbraco.Textbox"
    },
    "bannerLinkText":{
      "title":"Banner Link Text",
      "value":"Ok, then what is uHateoas  ?",
      "type":"text",
      "propertyEditor":"Umbraco.Textbox"
    },
    "bannerText":{
      "title":"Banner Text",
      "value":"<p><strong>HATEOAS</strong>, an abbreviation for Hypermedia as the Engine of Application State, is a constraint of the <strong>REST</strong> application architecture that distinguishes it from most other network application architectures. The principle is that a client interacts with a network application entirely through hypermedia provided dynamically by application servers. </p>\r\n<p>A <strong>REST</strong> client needs no prior knowledge about how to interact with any particular application or server beyond a generic understanding of hypermedia. By contrast, in a service-oriented architecture (SOA), clients and servers interact through a fixed interface shared through documentation or an interface description language (IDL). The <strong>HATEOAS</strong> constraint decouples client and server in a way that allows the server functionality to evolve independently.</p>",
      "type":"text",
      "propertyEditor":"Umbraco.TinyMCEv3"
    }
  }
}
//truncated for brevity
      
```
###text/json
```http
http://www.mysite.com/anypath/anyresource
```
(Content-Type : **text/json**) the current resource is output as an json formatted document. 
   
```json   
{
  "class":"umbHomePage",
  "title":"uHateoas",
  "properties":{
    "BannerHeader":"What is HATEOAS ?",
    "BannerLinkText":"Ok, then what is uHateoas  ?",
    "BannerText":"<p><strong>HATEOAS</strong>, an abbreviation for Hypermedia as the Engine of Application State, is a constraint of the <strong>REST</strong> application architecture that distinguishes it from most other network application architectures. The principle is that a client interacts with a network application entirely through hypermedia provided dynamically by application servers. </p>\r\n<p>A <strong>REST</strong> client needs no prior knowledge about how to interact with any particular application or server beyond a generic understanding of hypermedia. By contrast, in a service-oriented architecture (SOA), clients and servers interact through a fixed interface shared through documentation or an interface description language (IDL). The <strong>HATEOAS</strong> constraint decouples client and server in a way that allows the server functionality to evolve independently.</p>",
    "Byline":"Hypermedia as the Engine of Application State for Umbraco",
    "ContinueButtonText":"What does HATEOAS mean?",
    "Copyright":"WG2K",
    "CreateDate":"2015-06-03T11:00:07",
    "CreatorId":0,
    "CreatorName":"Michael Galloway",
    "DocumentTypeAlias":"umbHomePage",
    "DocumentTypeId":1137,
    "DribbbleLink":"",
    "FacebookLink":"http://facebook.com/uHateoas",
    "GoogleLink":"https://google.com/+uHateoas",
    "HideBanner":false,
    "Id":1145,
    "IsDraft":false,
    "ItemType":0,
    "Level":1,
    "LinkedInLink":"",
    "Name":"uHateoas",
    "Path":"-1,1145",
    "PinterestLink":"",
    "SiteName":"uHateoas",
    "SortOrder":0,
    "TemplateId":1167,
    "TwitterLink":"http://twitter.com/uHateoas",
    "UpdateDate":"2015-06-15T13:48:13",
    "Url":"/",
    "UrlName":"uhateoas",
    "WriterId":3,
    "WriterName":"brendan"
  }
}
```

###text/xml
```http
http://www.mysite.com/anypath/anyresource 
```
(Content-Type : **text/xml**) the current resource is output as an xml formatted document.     
   
```xml
<root>
  <class>umbHomePage
  </class>
  <title>uHateoas
  </title>
  <properties>
    <BannerHeader>What is HATEOAS ?
    </BannerHeader>
    <BannerLinkText>Ok, then what is uHateoas  ?
    </BannerLinkText>
    <BannerText>&lt;p&gt;&lt;strong&gt;HATEOAS&lt;/strong&gt;, an abbreviation for Hypermedia as the Engine of Application State, is a constraint of the &lt;strong&gt;REST&lt;/strong&gt; application architecture that distinguishes it from most other network application architectures. The principle is that a client interacts with a network application entirely through hypermedia provided dynamically by application servers. &lt;/p&gt;
      &lt;p&gt;A &lt;strong&gt;REST&lt;/strong&gt; client needs no prior knowledge about how to interact with any particular application or server beyond a generic understanding of hypermedia. By contrast, in a service-oriented architecture (SOA), clients and servers interact through a fixed interface shared through documentation or an interface description language (IDL). The &lt;strong&gt;HATEOAS&lt;/strong&gt; constraint decouples client and server in a way that allows the server functionality to evolve independently.&lt;/p&gt;
    </BannerText>
    <Byline>Hypermedia as the Engine of Application State for Umbraco
    </Byline>
    <ContinueButtonText>What does HATEOAS mean?
    </ContinueButtonText>
    <Copyright>WG2K
    </Copyright>
    <CreateDate>2015-06-03T11:00:07
    </CreateDate>
    <CreatorId>0
    </CreatorId>
    <CreatorName>Michael Galloway
    </CreatorName>
    <!-- truncated for brevity -->
```
#Switches

This is where the API really shines. It can be used to query, segment, filter and page your content in a SQL/LINQ like way using simple name-value pair switches.

The switches can be applied in any order as part of a simple query-string.

- **children** : 
```http
http://www.mysite.com/anypath?children=true
```
returns all the child nodes of the current node serialised as per the Content-Type

- **descendants** : 
```http
http://www.mysite.com/anypath?descendants=NewsItem
```
returns all the descendant nodes of the current node serialised as per the Content-Type and can also be comma separated to include multiple DocTypes : 
```http
http://www.mysite.com/anypath?descendants=NewsItem,BlogPost,FAQ
```
- **where** / and / or : 
```http
http://www.mysite.com/anypath?descendants=NewsItem&where=Summary.Contains('News Flash') and Edition lt 20 or Headline eq true
```
returns all NewsItems that are descendants of the current node where the Summary property contains the phrase News Flash

Supported operators include :
+ eq : =
+ ne : !=
+ ge : >=
+ gt : >
+ le : <=
+ lt : <

- **select** : 
```http
http://www.mysite.com/anypath?select=Name,Summary,Description
```
returns only the properties specified in the select statement

- **resolvecontent** : 
```http
http://www.mysite.com/anypath?resolvecontent=RelatedArticles
```
resolves the comma separated nodes stored in the RelatedArticles property and outputs them as part of an entities array of nodes

- **resolvemedia** : 
```http
http://www.mysite.com/anypath?resolvemedia=PageImage,HeaderImage
```
resolves the media node and replaces the property value with the resolved NiceUrl

- **skip** & **take** : 
```http
http://www.mysite.com/anypath?descendants=NewsItem&skip=8&take=4
```
use these switches for paging the data server-side

Of course they can all be used together to create really useful data retrieval scenarios eg : 

```http
http://www.mysite.com/anypath?descendants=NewsItem&where=Summary.Contains('News Flash')&resolvemedia=PageImage,NewsImage&skip=8&take=4
```
#Editing Umbraco

The **POST, PUT, PATCH** and **DELETE** verbs allow you to do Create Update and Delete operations using IContent via the Content Service when logged in as an Umbraco Back-Office User.

The API exposes Actions based on the permissions of the logged-in Umbraco User. **Please Note** : **PUT, PATCH** and **DELETE** verb support has only been enabled in **version 1.3.2**.

The actions Create, Update and Delete are exposed as a collection of actions based on the User's admin rights and the DocumentTypes that are allowed under the current node. With the Umbraco Hypermedia API, you are able to create complex Single Page Applications without writing a single line of server-side code. It turns any Umbraco v7+ website into a REST based data repository with a very capable, query-able Hypermedia API!

We are working on a build where you will be able to map Umbraco Member roles to Umbraco Users as well as mapping anonymous visitors to an anonymous Umbraco User to facilitate creation of nodes for use cases like ContactUs, Comment etc where we would like to enable visitors that are not logged in to be able to use the POST and PATCH verbs in a controlled manner. 
