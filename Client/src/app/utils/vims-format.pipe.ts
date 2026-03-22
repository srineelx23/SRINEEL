import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'vimsFormat',
  standalone: true
})
export class VimsFormatPipe implements PipeTransform {
  transform(value: any): string {
    if (!value) return '';
    let str = value.toString();
    
    // EVThreeWheeler -> EV-ThreeWheeler
    let result = str.replace(/^EV([A-Z])/, 'EV-$1');
    
    // EV-ThreeWheeler -> EV-Three Wheeler
    result = result.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
    
    // Special handling for common VIMS enums if any unique rules exist
    // Currently the regex handles most: TwoWheeler -> Two Wheeler, ThirdParty -> Third Party, etc.
    
    return result.trim();
  }
}
