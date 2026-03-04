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
            if (firstKey && Array.isArray(errorBody.errors[firstKey])) {
                return errorBody.errors[firstKey][0];
            }
        }

        // Common message fields
        return errorBody.message ||
            errorBody.Message ||
            errorBody.error ||
            errorBody.Error ||
            (typeof errorBody.title === 'string' ? errorBody.title : JSON.stringify(errorBody));
    }

    return String(errorBody);
}
