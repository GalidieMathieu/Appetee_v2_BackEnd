using Appetee.Application.Models.MealPlans;
using Appetee.Application.utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Diagnostics;

namespace Appetee.Api.Helpers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class MealPlannerBoundaryAttribute : ActionFilterAttribute
{
    public MealPlannerBoundaryAttribute()=>Order=-3001;
    public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var started=Stopwatch.GetTimestamp();
        var request=context.HttpContext.Request;
        var isGet=HttpMethods.IsGet(request.Method);
        var isDelete=HttpMethods.IsDelete(request.Method);
        if(request.Query.Any(p=>!isGet || p.Key is not ("from" or "through") || p.Value.Count!=1))
            throw MealPlanRules.InvalidPlanField("query","Unsupported or repeated query parameters.");
        if((isGet || isDelete) && (request.ContentLength>0 || request.Headers.ContainsKey("Transfer-Encoding")))
            throw MealPlanRules.InvalidPlanField("request","This operation does not accept a body.");
        if(!isGet)_=MealPlanRevisionHeader.Parse(request.Headers);
        if(!context.ModelState.IsValid && !context.ModelState.Values.SelectMany(v=>v.Errors).Any(e=>e.Exception is UnsupportedContentTypeException))
        {
            var errors=context.ModelState.Where(e=>e.Value!.Errors.Count>0).GroupBy(e=>FieldPath(e.Key),StringComparer.Ordinal)
                .ToDictionary(group=>group.Key,_=>new[]{"The field is missing or invalid."},StringComparer.Ordinal);
            throw new ValidationException("Meal planner validation failed.","validation_failed",errors);
        }
        var executed=await next();
        if(executed.Exception is {} exception && !executed.ExceptionHandled && exception is not (ApiException or OperationCanceledException))
            executed.Exception=new InternalServerException();
        var status=executed.Exception is ApiException api ? api.StatusCode :
            executed.Exception is not null && !executed.ExceptionHandled ? 500 :
            (executed.Result as ObjectResult)?.StatusCode ?? (executed.Result as StatusCodeResult)?.StatusCode ?? context.HttpContext.Response.StatusCode;
        context.HttpContext.RequestServices.GetRequiredService<ILogger<MealPlannerBoundaryAttribute>>().LogInformation(
            "Planner operation completed. Operation {Operation}; StatusCode {StatusCode}; ElapsedMs {ElapsedMs}; TraceId {TraceId}",
            context.ActionDescriptor.RouteValues["action"],status,Stopwatch.GetElapsedTime(started).TotalMilliseconds,context.HttpContext.TraceIdentifier);
    }
    private static string FieldPath(string key)
    {
        key=key.TrimStart('$','.');
        if(key.Length==0 || key=="request")return "request";
        return string.Join('.',key.Split('.').Select(p=>p.Length==0 ? p : char.ToLowerInvariant(p[0])+p[1..]));
    }
}
