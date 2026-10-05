using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace X_ChemicalStorage.BioBank.Security
{
    /// الأكشن يشتغل بس لو "IoT:TestConsole": true. غير كده 404 (للـ test-ingest / test-link).
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class TestModeOnlyAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var cfg = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            if (!string.Equals(cfg["IoT:TestConsole"], "true", StringComparison.OrdinalIgnoreCase))
                context.Result = new NotFoundResult();
        }
        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }

    /// طلب HTTP ingest بيحتاج Header: X-Api-Key يطابق "IoT:IngestApiKey". لو المفتاح مش متظبط الأكشن معطّل (404).
    [AttributeUsage(AttributeTargets.Method)]
    public class IngestApiKeyAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var cfg = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expected = cfg["IoT:IngestApiKey"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Result = new NotFoundResult();
                return;
            }

            var provided = context.HttpContext.Request.Headers["X-Api-Key"].ToString();
            var a = Encoding.UTF8.GetBytes(expected);
            var b = Encoding.UTF8.GetBytes(provided ?? "");
            if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
                context.Result = new UnauthorizedResult();
        }
        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }
}