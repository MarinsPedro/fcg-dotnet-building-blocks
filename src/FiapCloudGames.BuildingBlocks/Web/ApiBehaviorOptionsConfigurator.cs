using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FiapCloudGames.BuildingBlocks.Web;

internal static class ApiBehaviorOptionsConfigurator
{
    public static void Configure(ApiBehaviorOptions options)
    {
        options.SuppressMapClientErrors = true;
        options.InvalidModelStateResponseFactory = actionContext =>
        {
            var modelErrors = actionContext.ModelState
                .Where(item => item.Value is { Errors.Count: > 0 })
                .SelectMany(item => item.Value!.Errors.Select(error =>
                    new ModelErrorDescriptor(
                        error,
                        NormalizeFieldName(item.Key),
                        item.Key.StartsWith('$') ||
                        error.Exception is JsonException ||
                        error.Exception?.InnerException is JsonException)))
                .ToArray();

            ApiError[] errors = modelErrors.Any(item => item.IsJson)
                ?
                [
                    new ApiError
                    {
                        Message = "O JSON enviado é inválido."
                    }
                ]
                : modelErrors
                    .Select(item => new ApiError
                    {
                        Message = ToValidationMessage(item.Error),
                        Field = item.Field
                    })
                    .DistinctBy(error => (error.Message, error.Field))
                    .ToArray();

            var result = new ObjectResult(
                ApiProblemDetailsFactory.CreateValidation(
                    actionContext.HttpContext,
                    errors))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

            result.ContentTypes.Add(
                FiapApiDefaultsExtensions.ProblemJsonContentType);
            return result;
        };
    }

    internal static string? NormalizeFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || fieldName == "$")
        {
            return null;
        }

        var normalized = fieldName.StartsWith('$')
            ? fieldName[1..].TrimStart('.')
            : fieldName;

        if (normalized.Length == 0)
        {
            return null;
        }

        return string.Join(
            '.',
            normalized
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(JsonNamingPolicy.CamelCase.ConvertName));
    }

    private static string ToValidationMessage(ModelError error)
    {
        if (error.Exception is JsonException)
        {
            return "O JSON enviado é inválido.";
        }

        return string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? "O valor informado é inválido."
            : error.ErrorMessage;
    }

    private sealed record ModelErrorDescriptor(
        ModelError Error,
        string? Field,
        bool IsJson);
}
