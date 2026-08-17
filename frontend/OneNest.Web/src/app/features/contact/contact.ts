import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { finalize } from 'rxjs';

import { ContactService } from '../../services/contact.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Spinner } from '../../shared/spinner/spinner';
import {
  ContactMessage,
  ContactCategory,
  ContactStatus,
  CONTACT_CATEGORY_LABELS,
  CONTACT_STATUS_LABELS
} from '../../models/contact.model';

@Component({
  selector: 'app-contact',
  imports: [ReactiveFormsModule, DatePipe, Spinner],
  templateUrl: './contact.html',
  styleUrl: './contact.css'
})
export class Contact implements OnInit {
  private readonly contactService = inject(ContactService);
  private readonly fb = inject(FormBuilder);
  private readonly toastService = inject(ToastService);

  readonly ContactCategory = ContactCategory;
  readonly ContactStatus = ContactStatus;
  readonly categoryLabels = CONTACT_CATEGORY_LABELS;
  readonly statusLabels = CONTACT_STATUS_LABELS;

  readonly categories = [
    ContactCategory.General,
    ContactCategory.Support,
    ContactCategory.Bug,
    ContactCategory.Feedback
  ];

  readonly messages = signal<ContactMessage[]>([]);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly expandedId = signal<string | null>(null);

  readonly contactForm = this.fb.group({
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    category: [ContactCategory.General, Validators.required],
    message: ['', [Validators.required, Validators.maxLength(2000)]]
  });

  readonly newCount = computed(() =>
    this.messages().filter(m => m.status === ContactStatus.New).length
  );

  readonly resolvedCount = computed(() =>
    this.messages().filter(m => m.status === ContactStatus.Resolved).length
  );

  ngOnInit(): void {
    this.loadMessages();
  }

  private loadMessages(): void {
    this.isLoading.set(true);
    this.contactService.getMyMessages()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: messages => this.messages.set(messages),
        error: () => this.toastService.error('Failed to load messages')
      });
  }

  submit(): void {
    if (this.contactForm.invalid) {
      this.contactForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.contactService.create({
      subject: this.contactForm.value.subject!,
      category: Number(this.contactForm.value.category),
      message: this.contactForm.value.message!
    })
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: () => {
          this.contactForm.reset({ category: ContactCategory.General });
          this.loadMessages();
          this.toastService.success('Message sent successfully!');
        },
        error: () => this.toastService.error('Failed to send message')
      });
  }

  toggleExpand(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  statusClass(status: ContactStatus): string {
    switch (status) {
      case ContactStatus.New: return 'badge-new';
      case ContactStatus.Read: return 'badge-read';
      case ContactStatus.Resolved: return 'badge-resolved';
    }
  }
}
