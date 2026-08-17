import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { finalize } from 'rxjs';

import { AdminService } from '../../services/admin.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast/toast.service';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import { Spinner } from '../../shared/spinner/spinner';
import {
  ContactMessage,
  ContactStatus,
  CONTACT_CATEGORY_LABELS,
  CONTACT_STATUS_LABELS,
  UpdateContactStatusRequest
} from '../../models/contact.model';
import { AdminUser } from '../../models/admin.model';

type Tab = 'messages' | 'users';

@Component({
  selector: 'app-admin',
  imports: [ReactiveFormsModule, DatePipe, Spinner],
  templateUrl: './admin.html',
  styleUrl: './admin.css'
})
export class Admin implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly authService = inject(AuthService);
  private readonly toastService = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  private readonly fb = inject(FormBuilder);

  readonly ContactStatus = ContactStatus;
  readonly categoryLabels = CONTACT_CATEGORY_LABELS;
  readonly statusLabels = CONTACT_STATUS_LABELS;
  readonly statuses = [ContactStatus.New, ContactStatus.Read, ContactStatus.Resolved];

  readonly activeTab = signal<Tab>('messages');

  // ── Messages ───────────────────────────────────────────────
  readonly messages = signal<ContactMessage[]>([]);
  readonly messagesLoading = signal(false);
  readonly statusFilter = signal<ContactStatus | 'all'>('all');
  readonly expandedMessageId = signal<string | null>(null);
  readonly replyingId = signal<string | null>(null);
  readonly isSavingReply = signal(false);

  readonly replyForm = this.fb.group({
    status: [ContactStatus.Read, Validators.required],
    adminReply: ['']
  });

  readonly filteredMessages = computed(() => {
    const filter = this.statusFilter();
    if (filter === 'all') return this.messages();
    return this.messages().filter(m => m.status === filter);
  });

  readonly newCount = computed(() =>
    this.messages().filter(m => m.status === ContactStatus.New).length
  );

  // ── Users ──────────────────────────────────────────────────
  readonly users = signal<AdminUser[]>([]);
  readonly usersLoading = signal(false);
  readonly userSearch = signal('');
  readonly updatingUserId = signal<string | null>(null);

  readonly currentUserId = computed(() => this.authService.currentUser()?.userId ?? '');

  readonly filteredUsers = computed(() => {
    const search = this.userSearch().toLowerCase().trim();
    if (!search) return this.users();
    return this.users().filter(u =>
      u.fullName.toLowerCase().includes(search) ||
      u.email.toLowerCase().includes(search)
    );
  });

  ngOnInit(): void {
    this.loadMessages();
    this.loadUsers();
  }

  setTab(tab: Tab): void {
    this.activeTab.set(tab);
  }

  // ── Messages logic ─────────────────────────────────────────

  private loadMessages(): void {
    this.messagesLoading.set(true);
    this.adminService.getAllMessages()
      .pipe(finalize(() => this.messagesLoading.set(false)))
      .subscribe({
        next: msgs => this.messages.set(msgs),
        error: () => this.toastService.error('Failed to load messages')
      });
  }

  toggleMessage(id: string): void {
    if (this.expandedMessageId() === id) {
      this.expandedMessageId.set(null);
      this.replyingId.set(null);
    } else {
      this.expandedMessageId.set(id);
      this.replyingId.set(null);
    }
  }

  openReplyForm(msg: ContactMessage): void {
    this.replyingId.set(msg.id);
    this.replyForm.patchValue({
      status: msg.status,
      adminReply: msg.adminReply ?? ''
    });
  }

  cancelReply(): void {
    this.replyingId.set(null);
  }

  saveReply(id: string): void {
    if (this.replyForm.invalid) {
      this.replyForm.markAllAsTouched();
      return;
    }

    const request: UpdateContactStatusRequest = {
      status: Number(this.replyForm.value.status),
      adminReply: this.replyForm.value.adminReply?.trim() || undefined
    };

    this.isSavingReply.set(true);
    this.adminService.updateMessageStatus(id, request)
      .pipe(finalize(() => this.isSavingReply.set(false)))
      .subscribe({
        next: updated => {
          this.messages.update(list =>
            list.map(m => m.id === id ? updated : m)
          );
          this.replyingId.set(null);
          this.toastService.success('Message updated');
        },
        error: () => this.toastService.error('Failed to update message')
      });
  }

  statusClass(status: ContactStatus): string {
    switch (status) {
      case ContactStatus.New: return 'badge-new';
      case ContactStatus.Read: return 'badge-read';
      case ContactStatus.Resolved: return 'badge-resolved';
    }
  }

  // ── Users logic ────────────────────────────────────────────

  private loadUsers(): void {
    this.usersLoading.set(true);
    this.adminService.getAllUsers()
      .pipe(finalize(() => this.usersLoading.set(false)))
      .subscribe({
        next: users => this.users.set(users),
        error: () => this.toastService.error('Failed to load users')
      });
  }

  async toggleRole(user: AdminUser): Promise<void> {
    if (user.id === this.currentUserId()) {
      this.toastService.error('You cannot change your own role');
      return;
    }

    const newRole = user.role === 'Admin' ? 'User' : 'Admin';
    const confirmed = await this.confirmService.confirm({
      title: `Make ${newRole}`,
      message: `Are you sure you want to change ${user.fullName}'s role to ${newRole}?`,
      confirmText: 'Confirm',
      cancelText: 'Cancel'
    });

    if (!confirmed) return;

    this.updatingUserId.set(user.id);
    this.adminService.updateUserRole(user.id, { role: newRole })
      .pipe(finalize(() => this.updatingUserId.set(null)))
      .subscribe({
        next: updated => {
          this.users.update(list =>
            list.map(u => u.id === user.id ? updated : u)
          );
          this.toastService.success(`${user.fullName} is now ${newRole}`);
        },
        error: () => this.toastService.error('Failed to update role')
      });
  }
}
