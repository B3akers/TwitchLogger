using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.ActionFilters
{
    public class LoginActionFilter : IAsyncActionFilter
    {
        private IUserAuthentication _userAuthentication;

        public LoginActionFilter(IUserAuthentication userAuthentication)
        {
            _userAuthentication = userAuthentication;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var account = await _userAuthentication.GetAuthenticatedUser(context.HttpContext);
            if (account != null)
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Admin", action = "Index" })) { Permanent = false };
                return;
            }

            await next();
        }
    }
}
