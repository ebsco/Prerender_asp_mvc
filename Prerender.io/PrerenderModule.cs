using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Prerender.io
{
    public class PrerenderModule : IHttpModule
    {
        private HttpApplication _context;
	   private PrerenderIoCommon _prerenderIoCommon;

	   public PrerenderModule()
	   {
		   UseDefaultUserAgents = true;
		   UseDefaultExtensionsToIgnore = true;
	   }

        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            this._context = context;
		  _prerenderIoCommon = new PrerenderIoCommon(UseDefaultUserAgents, UseDefaultExtensionsToIgnore);

            context.BeginRequest += context_BeginRequest;
        }

        protected void context_BeginRequest(object sender, EventArgs e)
        {
            try
            {
                DoPrerender(_context);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.ToString());
            }
        }

        private void DoPrerender(HttpApplication context)
        {
            var httpContext = context.Context;
            var request = httpContext.Request;
            var response = httpContext.Response;
		  var referer = request.UrlReferrer == null ? string.Empty : request.UrlReferrer.AbsoluteUri;

		  if (_prerenderIoCommon.ShouldShowPrerenderedPage(request.Url, request.QueryString, request.UserAgent, referer))
            {
			  var result = _prerenderIoCommon.GetPrerenderedPageResponse(request.Url, request.UserAgent);

                response.StatusCode = (int)result.StatusCode;
			 _prerenderIoCommon.WriteHeaders(response.Headers, result.Headers);
      
                response.Write(result.ResponseBody);
                response.Flush();
                context.CompleteRequest();
            }
        }

	   /// <summary>
	   /// This indicates whether to load the hard coded default User Agents or only use the ones on the web.config file
	   /// </summary>
	   public bool UseDefaultUserAgents { get; private set; }

	   /// <summary>
	   /// This indicates whether to load the hard coded Extensions to Ignore or only use the ones on the web.config file
	   /// </summary>
	   public bool UseDefaultExtensionsToIgnore { get; private set; }
    }
}
