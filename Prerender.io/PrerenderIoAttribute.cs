using System;
using System.Text;
using System.Web.Mvc;

namespace Prerender.io
{
	/// <summary>
	/// This Attribute is designed to attach to a Method to check a specific MVC Endpoint.
	/// <remarks>
	/// This class is intentially left un-sealed as it can be inhereted to perform more/additional items before/after Prerender.IO runs.
	/// </remarks> 
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public class PrerenderIoAttribute : ActionFilterAttribute
	{
		private PrerenderIoCommon _prerenderIoCommon;

		/// <summary>
		/// Default Constructor
		/// </summary>
		public PrerenderIoAttribute() { }

		/// <summary>
		/// Constructor that specifies whether to use the default agents & extensions to ignore.
		/// </summary>
		/// <param name="useDefaultUserAgents">Whether to preload default user agents or only load whats in the web.config</param>
		/// <param name="useDefaultExtensionsToIgnore">Whether to preload extensions to ignore or only load whats in the web.config</param>
		public PrerenderIoAttribute(bool useDefaultUserAgents, bool useDefaultExtensionsToIgnore)
		{
			UseDefaultUserAgents = useDefaultUserAgents;
			UseDefaultExtensionsToIgnore = useDefaultExtensionsToIgnore;
		}

		/// <summary>
		/// This is called before the MVC endpoint code is called.
		/// </summary>
		public override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			_prerenderIoCommon = new PrerenderIoCommon(UseDefaultUserAgents, UseDefaultExtensionsToIgnore);

			var request = filterContext.RequestContext.HttpContext.Request;
			var referer = request.UrlReferrer == null ? string.Empty : request.UrlReferrer.AbsoluteUri;

			if (_prerenderIoCommon.ShouldShowPrerenderedPage(request.Url, request.QueryString, request.UserAgent, referer))
			{
				// Start Prerender here.
				var result = _prerenderIoCommon.GetPrerenderedPageResponse(request.Url, request.UserAgent, filterContext.HttpContext.Response.Headers, request.ApplicationPath);

				filterContext.HttpContext.Response.StatusCode = (int)result.StatusCode;
				_prerenderIoCommon.WriteHeaders(filterContext.HttpContext.Response.Headers, result.Headers);

				// This will send the response to the client and the {interface}/home or whatever controller is being hit will never be called.
				filterContext.Result = new ContentResult()
				{
					Content = result.ResponseBody,
					ContentType = "text/html; charset=utf-8",
					ContentEncoding = Encoding.UTF8,
				};
			}

			base.OnActionExecuting(filterContext);
		}

		

		/// <summary>
		/// This indicates whether to load the hard coded default User Agents or only use the ones on the web.config file
		/// </summary>
		public bool UseDefaultUserAgents { get; set; }

		/// <summary>
		/// This indicates whether to load the hard coded Extensions to Ignore or only use the ones on the web.config file
		/// </summary>
		public bool UseDefaultExtensionsToIgnore { get; set; }
	}
}
