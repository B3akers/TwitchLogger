using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.ActionFilters
{
    public class AdminActionFilter : IAsyncActionFilter
    {
        private IUserAuthentication _userAuthentication;

        public AdminActionFilter(IUserAuthentication userAuthentication)
        {
            _userAuthentication = userAuthentication;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var account = await _userAuthentication.GetAuthenticatedUser(context.HttpContext);

            if (account == null)
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Login", action = "Index" })) { Permanent = false };
                return;
            }

            context.HttpContext.Items["userAccount"] = account;

            if (!account.IsAdmin && !account.IsModerator)
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Home", action = "Index" })) { Permanent = false };
                return;
            }

            await next();
        }
    }
}
