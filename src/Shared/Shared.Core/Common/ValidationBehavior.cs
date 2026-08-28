using FluentValidation;
using MediatR;
using System.Reflection;

namespace Shared.Core.Common;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Count != 0)
            {
                if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var resultType = typeof(TResponse).GetGenericArguments()[0];
                    var errorInfos = failures.Select(f => new ErrorInfo(f.ErrorMessage, f.PropertyName)).ToList();
                    
                    var failMethod = typeof(Result<>)
                        .MakeGenericType(resultType)
                        .GetMethod("Fail", new[] { typeof(List<ErrorInfo>), typeof(string) });
                        
                    return (TResponse)failMethod!.Invoke(null, new object?[] { errorInfos, null })!;
                }

                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
