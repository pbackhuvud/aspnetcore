// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.Infrastructure;

/// <summary>
/// A <see cref="IActionResultExecutor{RedirectToPageResult}"/> for <see cref="RedirectToPageResult"/>.
/// </summary>
public class RedirectToPageResultExecutor : IActionResultExecutor<RedirectToPageResult>
{
    private readonly ILogger _logger;
    private readonly IUrlHelperFactory _urlHelperFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="RedirectToPageResultExecutor"/>.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="urlHelperFactory">The factory used to create url helpers.</param>
    public RedirectToPageResultExecutor(ILoggerFactory loggerFactory, IUrlHelperFactory urlHelperFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        if (urlHelperFactory == null)
        {
            throw new ArgumentNullException(nameof(urlHelperFactory));
        }

        _logger = loggerFactory.CreateLogger<RedirectToRouteResult>();
        _urlHelperFactory = urlHelperFactory;
    }

    /// <inheritdoc />
    public virtual Task ExecuteAsync(ActionContext context, RedirectToPageResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var urlHelper = result.UrlHelper ?? _urlHelperFactory.GetUrlHelper(context);
        var destinationUrl = urlHelper.Page(
            result.PageName,
            result.PageHandler,
            result.RouteValues,
            result.Protocol,
            result.Host,
            fragment: result.Fragment);

        if (string.IsNullOrEmpty(destinationUrl))
        {
            throw new InvalidOperationException(Resources.FormatNoRoutesMatchedForPage(result.PageName));
        }

        _logger.RedirectToPageResultExecuting(result.PageName);

        if (result.PreserveMethod)
        {
            context.HttpContext.Response.StatusCode = result.Permanent ?
                StatusCodes.Status308PermanentRedirect : StatusCodes.Status307TemporaryRedirect;
            context.HttpContext.Response.Headers.Location = destinationUrl;
        }
        else
        {
            context.HttpContext.Response.Redirect(destinationUrl, result.Permanent);
        }

        return Task.CompletedTask;
    }
}
