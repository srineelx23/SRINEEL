
/**
 * Formats camelCase or PascalCase enum values into human-readable spaced strings.
 * Specific rules for VIMS:
 * - Adds spaces before uppercase letters.
 */
export function formatEnum(value: any): string {
    if (!value) return '';
    let str = value.toString();
    
    // EVThreeWheeler -> EV-ThreeWheeler
    let result = str.replace(/^EV([A-Z])/, 'EV-$1');
    
    // EV-ThreeWheeler -> EV-Three Wheeler
    result = result.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
    
    // Handle special acronyms or abbreviations
    result = result.replace(/^Three Wheeler$/, 'Three Wheeler'); // No change
    
    return result.trim();
}
