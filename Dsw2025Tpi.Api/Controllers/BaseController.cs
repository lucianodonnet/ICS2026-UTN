using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dsw2025Tpi.Api.Controllers
{
    public abstract class BaseController: Controller
    {
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if(context.Exception != null)
            {
                context.Result = BadRequest();
            }
            base.OnActionExecuted(context);
        }

        //private IActionResult HandlerException(Exception exception)
        //{
            // todo el middleware 
        //}
    }
}

/*
 {
   code = "codigo que no es el http"
   message = "mensaje de error"
  }
 */