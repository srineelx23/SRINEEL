using Microsoft.AspNetCore.Diagnostics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VIMS.Application.Exceptions;
namespace VIMS.API.Exceptions
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, exception.Message);

            int statusCode = StatusCodes.Status500InternalServerError;

            if (exception is AppException appException)
            {
                statusCode = appException.StatusCode;
            }

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = exception.Message,
                statusCode
            };

            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(response),
                cancellationToken
            );

            return true; // Exception handled
        }
    }
}
