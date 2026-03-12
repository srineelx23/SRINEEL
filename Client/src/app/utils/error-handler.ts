import { HttpErrorResponse } from '@angular/common/http';

export function extractErrorMessage(err: any): string {
    if (!err) return 'An unexpected error occurred';

    // If it's a native HttpErrorResponse, inspect the 'error' property
    const errorBody = err instanceof HttpErrorResponse ? err.error : err;

    if (!errorBody) return err.message || 'An unexpected error occurred';

    // If string, try to parse JSON
    if (typeof errorBody === 'string') {
        try {
            const parsed = JSON.parse(errorBody);
            return parsed.message || parsed.Message || parsed.error || parsed.Error || errorBody;
        } catch {
            return errorBody;
        }
    }

    // If object, check various common message properties
    if (typeof errorBody === 'object' && errorBody !== null) {
        // Handle ASP.NET Core Identity/Validation errors collections
        if (errorBody.errors && typeof errorBody.errors === 'object') {
            const firstKey = Object.keys(errorBody.errors)[0];
            if (firstKey) {
                const errorVal = errorBody.errors[firstKey];
                if (Array.isArray(errorVal)) return errorVal[0];
                if (typeof errorVal === 'string') return errorVal;
            }
        }

        // Check for common error fields in order of preference
        const message = errorBody.message ||
            errorBody.Message ||
            errorBody.error ||
            errorBody.Error ||
            errorBody.statusText ||
            errorBody.title;
        
        if (typeof message === 'string') return message;

        // If no string found, try to find any string property that isn't technical
        const stringProps = Object.keys(errorBody)
            .filter(key => !['status', 'statusCode', 'ok', 'url', 'name', 'headers'].includes(key.toLowerCase()))
            .map(key => errorBody[key])
            .filter(val => typeof val === 'string');

        if (stringProps.length > 0) return stringProps[0];

        // Fallback to title if it exists even if it's not a string (though unlikely)
        if (errorBody.title) return String(errorBody.title);
    }

    return 'An unexpected system error occurred';
}
