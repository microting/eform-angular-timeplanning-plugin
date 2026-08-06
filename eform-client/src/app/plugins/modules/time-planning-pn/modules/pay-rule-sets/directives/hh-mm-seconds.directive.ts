import {Directive, ElementRef, HostListener, Renderer2, forwardRef} from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from '@angular/forms';
import {parseHhMmToSeconds, secondsToHhMmInput} from '../pay-rule-format.util';

/**
 * Value accessor that lets a text input edit a duration as hours:minutes
 * (e.g. '7:24' or '07:24') while the bound form control keeps storing the
 * duration in SECONDS.
 *
 * An empty field means "unlimited" and maps to a null control value.
 * Malformed text sets an `hhMmFormat` error on the control, which keeps the
 * surrounding form invalid so it cannot be saved. Malformed text never writes
 * to the control: pushing null would be indistinguishable from the deliberate
 * "unlimited" value and would make the field claim to be unlimited while the
 * user is still typing.
 */
@Directive({
  selector: 'input[appHhMmSeconds]',
  standalone: false,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => HhMmSecondsDirective),
      multi: true,
    },
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => HhMmSecondsDirective),
      multi: true,
    },
  ],
})
export class HhMmSecondsDirective implements ControlValueAccessor, Validator {
  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};
  private onValidatorChange: () => void = () => {};
  private malformed = false;

  constructor(
    private elementRef: ElementRef<HTMLInputElement>,
    private renderer: Renderer2
  ) {}

  writeValue(value: number | null): void {
    const wasMalformed = this.malformed;
    this.malformed = false;
    this.renderer.setProperty(this.elementRef.nativeElement, 'value', secondsToHhMmInput(value));
    if (wasMalformed) {
      // A programmatic write replaces whatever the user typed, so the format
      // error must not survive it. This also covers a directive instance being
      // re-bound to another control when a table row is removed.
      this.onValidatorChange();
    }
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  registerOnValidatorChange(fn: () => void): void {
    this.onValidatorChange = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.renderer.setProperty(this.elementRef.nativeElement, 'disabled', isDisabled);
  }

  @HostListener('input', ['$event.target.value'])
  onInput(raw: string): void {
    const text = (raw || '').trim();
    if (text === '') {
      // Empty means unlimited.
      this.malformed = false;
      this.onValidatorChange();
      this.onChange(null);
      return;
    }
    const seconds = parseHhMmToSeconds(text);
    this.malformed = seconds === null;
    this.onValidatorChange();
    if (seconds === null) {
      // Malformed: leave the control value untouched. Writing null here would
      // read as "unlimited"; the hhMmFormat error keeps the form invalid, so
      // the stale value cannot be saved either.
      return;
    }
    this.onChange(seconds);
  }

  @HostListener('blur')
  onBlur(): void {
    this.onTouched();
    if (this.malformed) {
      // Keep the offending text visible so the user can correct it.
      return;
    }
    const text = (this.elementRef.nativeElement.value || '').trim();
    const seconds = text === '' ? null : parseHhMmToSeconds(text);
    this.renderer.setProperty(this.elementRef.nativeElement, 'value', secondsToHhMmInput(seconds));
  }

  validate(_control: AbstractControl): ValidationErrors | null {
    return this.malformed ? {hhMmFormat: true} : null;
  }
}
