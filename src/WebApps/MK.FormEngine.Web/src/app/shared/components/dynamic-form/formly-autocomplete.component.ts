import { Component, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldType, FieldTypeConfig, FormlyModule } from '@ngx-formly/core';
import { AutoCompleteModule } from 'primeng/autocomplete';

@Component({
  selector: 'formly-field-autocomplete',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormlyModule, AutoCompleteModule],
  template: `
    <p-autoComplete
      [formControl]="formControl"
      [formlyAttributes]="field"
      [suggestions]="filteredOptions"
      (completeMethod)="search($event)"
      [optionLabel]="labelProp"
      [optionValue]="valueProp"
      [placeholder]="props.placeholder || 'Select or type to search...'"
      [disabled]="props.disabled || false"
      [dropdown]="true"
      [multiple]="props['multiple'] || false"
      appendTo="body"
      styleClass="w-full"
      inputStyleClass="w-full"
    ></p-autoComplete>
  `
})
export class FormlyFieldAutocompleteComponent extends FieldType<FieldTypeConfig> implements OnInit, OnChanges {
  filteredOptions: any[] = [];

  get labelProp(): string {
    return this.props['labelProp'] || 'name';
  }

  get valueProp(): string {
    return this.props['valueProp'] || 'id';
  }

  ngOnInit() {
    this.initOptions();
  }

  ngOnChanges(changes: SimpleChanges) {
    this.initOptions();
  }

  private initOptions() {
    const rawOptions: any = this.props?.options;
    const options: any[] = Array.isArray(rawOptions) ? rawOptions : [];
    this.filteredOptions = [...options];
  }

  search(event: { query: string }) {
    const query = (event.query || '').toLowerCase();
    const rawOptions: any = this.props?.options;
    const options: any[] = Array.isArray(rawOptions) ? rawOptions : [];
    if (!query) {
      this.filteredOptions = [...options];
      return;
    }
    this.filteredOptions = options.filter(opt => {
      if (typeof opt === 'string') {
        return opt.toLowerCase().includes(query);
      }
      const label = opt[this.labelProp] || opt.name || opt.label || '';
      return String(label).toLowerCase().includes(query);
    });
  }
}

