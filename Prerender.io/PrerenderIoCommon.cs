using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Prerender.io
{
	/// <summary>
	/// This is the common Prerender.IO class that both the Module & Attribute will use
	/// </summary>
	class PrerenderIoCommon
	{
		private const string PRERENDER_SECTION_KEY = "Prerender";
		private const string ESCAPED_FRAGMENT = "_escaped_fragment_";

		private PrerenderConfigSection _prerenderConfig;

		/// <summary>
		/// This will create the object that does the bulk of the Prerender.IO Work
		/// </summary>
		/// <param name="useDefaultUserAgents">Whether to preload default user agents or only load whats in the web.config</param>
		/// <param name="useDefaultExtensionsToIgnore">Whether to preload extensions to ignore or only load whats in the web.config</param>
		internal PrerenderIoCommon(bool useDefaultUserAgents, bool useDefaultExtensionsToIgnore)
		{
			UseDefaultUserAgents = useDefaultUserAgents;
			UseDefaultExtensionsToIgnore = useDefaultExtensionsToIgnore;

			_prerenderConfig = ConfigurationManager.GetSection(PRERENDER_SECTION_KEY) as PrerenderConfigSection;
		}

		/// <summary>
		/// This will retrieve the Prerender.IO page.
		/// </summary>
		/// <param name="apiUrl">The URI of the application to retrieve</param>
		/// <param name="userAgent">The user agent to send.</param>
		/// <returns>The response of the request</returns>
		internal ResponseResult GetPrerenderedPageResponse(string apiUrl, string userAgent)
		{
			var webRequest = (HttpWebRequest)WebRequest.Create(apiUrl);
			webRequest.Method = "GET";
			webRequest.UserAgent = userAgent;
			SetProxy(webRequest);
			SetNoCache(webRequest);

			// Add our key!
			if (_prerenderConfig.Token.IsNotBlank())
			{
				webRequest.Headers.Add("X-Prerender-Token", _prerenderConfig.Token);
			}

			try
			{
				// Get the web response and read content etc. if successful
				var webResponse = (HttpWebResponse)webRequest.GetResponse();
				var reader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8);
				return new ResponseResult(webResponse.StatusCode, reader.ReadToEnd(), webResponse.Headers);
			}
			catch (WebException e)
			{
				// Handle response WebExceptions for invalid renders (404s, 504s etc.) - but we still want the content
				var reader = new StreamReader(e.Response.GetResponseStream(), Encoding.UTF8);
				return new ResponseResult(((HttpWebResponse)e.Response).StatusCode, reader.ReadToEnd(), e.Response.Headers);
			}
		}

		/// <summary>
		/// This will determine if the app should load prerender.
		/// </summary>
		/// <param name="url">The URI being requested</param>
		/// <param name="querystring">The querystring for the current request</param>
		/// <param name="userAgent">The current User Agent</param>
		/// <param name="referer">The referer URL</param>
		/// <returns>True indicating Prerender.IO should be used</returns>
		internal bool ShouldShowPrerenderedPage(Uri url, NameValueCollection querystring, string userAgent, string referer)
		{
			if (IsInResources(url))
			{
				return false;
			}

			if (userAgent.IsBlank())
			{
				return false;
			}

			if (IsInSearchUserAgent(userAgent))
			{
				return true;
			}

			if (HasEscapedFragment(querystring) && IsInSearchUserAgent(userAgent))
			{
				return true;
			}

			var whiteList = _prerenderConfig.Whitelist;
			if (whiteList != null && !IsInWhiteList(url, whiteList))
			{
				return false;
			}

			var blacklist = _prerenderConfig.Blacklist;
			if (blacklist != null && IsInBlackList(url, referer, blacklist))
			{
				return false;
			}

			return true;

		}

		/// <summary>
		/// Determine whether there is an escaped fragment in the Querystring
		/// </summary>
		/// <param name="querystring">The current querystring of the URL</param>
		/// <returns>True indicates that there is an escape fragment</returns>
		private bool HasEscapedFragment(NameValueCollection querystring)
		{
			return querystring.AllKeys.Contains(ESCAPED_FRAGMENT);
		}

		/// <summary>
		/// This will set an outbound proxy
		/// </summary>
		private void SetProxy(HttpWebRequest webRequest)
		{
			if (_prerenderConfig.Proxy != null && _prerenderConfig.Proxy.Url.IsNotBlank())
			{
				webRequest.Proxy = new WebProxy(_prerenderConfig.Proxy.Url, _prerenderConfig.Proxy.Port);
			}
		}

		/// <summary>
		/// Set no-cache
		/// </summary>
		private static void SetNoCache(HttpWebRequest webRequest)
		{
			webRequest.Headers.Add("Cache-Control", "no-cache");
			webRequest.ContentType = "text/html";
		}

		/// <summary>
		/// Whether the current request url or referer is in the blacklist
		/// </summary>
		/// <param name="url">Url requested</param>
		/// <param name="referer">The referer</param>
		/// <param name="blacklist">The blacklist</param>
		/// <returns></returns>
		private bool IsInBlackList(Uri url, string referer, IEnumerable<string> blacklist)
		{
			return blacklist.Any(item =>
			{
				var regex = new Regex(item);
				return regex.IsMatch(url.AbsoluteUri) || (referer.IsNotBlank() && regex.IsMatch(referer));
			});
		}

		/// <summary>
		/// Whether the current request url or referer is in the whitelist
		/// </summary>
		/// <param name="url">Url requested</param>
		/// <param name="whiteList">The blacklist</param>
		/// <returns></returns>
		private bool IsInWhiteList(Uri url, IEnumerable<string> whiteList)
		{
			return whiteList.Any(item => new Regex(item).IsMatch(url.AbsoluteUri));
		}

		#region User Agent

		/// <summary>
		/// Check for User Agent
		/// </summary>
		/// <param name="useAgent">Current User Agent</param>
		/// <returns>Current User Agent</returns>
		private bool IsInSearchUserAgent(string useAgent)
		{
			var crawlerUserAgents = GetCrawlerUserAgents();

			// We need to see if the user agent actually contains any of the partial user agents we have!
			// THE ORIGINAL version compared for an exact match...!
			return
			    (crawlerUserAgents.Any(
				   crawlerUserAgent =>
				   useAgent.IndexOf(crawlerUserAgent, StringComparison.InvariantCultureIgnoreCase) >= 0));
		}

		/// <summary>
		/// Retrieve the list of User Agents
		/// </summary>
		/// <returns></returns>
		private IEnumerable<String> GetCrawlerUserAgents()
		{
			var crawlerUserAgents = new List<string>();

			if (UseDefaultUserAgents)
			{
				crawlerUserAgents.AddRange(new[]
				{
					"bingbot", "baiduspider", "facebookexternalhit", "twitterbot", "yandex", "rogerbot",
					"linkedinbot", "embedly", "bufferbot", "quora link preview", "showyoubot", "outbrain"
				});
			}

			if (_prerenderConfig.CrawlerUserAgents.IsNotEmpty())
			{
				crawlerUserAgents.AddRange(_prerenderConfig.CrawlerUserAgents);
			}
			return crawlerUserAgents;
		}

		#endregion

		#region Resources

		/// <summary>
		/// Check if the requested URL is in the list of Resoruces
		/// </summary>
		/// <param name="url">URI requested</param>
		/// <returns>True indicates its a resource</returns>
		private bool IsInResources(Uri url)
		{
			var extensionsToIgnore = GetExtensionsToIgnore();
			return extensionsToIgnore.Any(item => url.AbsoluteUri.ToLower().Contains(item.ToLower()));
		}

		/// <summary>
		/// Retrieve the list of Extentions to ignore
		/// </summary>
		/// <returns>A collection of all the extensions to ignore</returns>
		private IEnumerable<String> GetExtensionsToIgnore()
		{
			var extensionsToIgnore = new List<string>();

			if (UseDefaultExtensionsToIgnore)
			{
				extensionsToIgnore.AddRange(new[]{".js", ".css", ".less", ".png", ".jpg", ".jpeg",
					".gif", ".pdf", ".doc", ".txt", ".zip", ".mp3", ".rar", ".exe", ".wmv", ".doc", ".avi", ".ppt", ".mpg",
					".mpeg", ".tif", ".wav", ".mov", ".psd", ".ai", ".xls", ".mp4", ".m4a", ".swf", ".dat", ".dmg",
					".iso", ".flv", ".m4v", ".torrent"});
			}
			
			if (_prerenderConfig.ExtensionsToIgnore.IsNotEmpty())
			{
				extensionsToIgnore.AddRange(_prerenderConfig.ExtensionsToIgnore);
			}
			return extensionsToIgnore;
		}

		#endregion

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
