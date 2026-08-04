import {
  AfterViewChecked,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { finalize } from 'rxjs';

import { AiService } from '../../services/ai.service';
import { AuthService } from '../../services/auth.service';
import {
  ChatMessageResponse,
  ConversationResponse,
  ConversationSummary
} from '../../models/ai.model';
import { ToastService } from '../../shared/toast/toast.service';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import { Spinner } from '../../shared/spinner/spinner';

@Component({
  selector: 'app-ai-assistant',
  imports: [ReactiveFormsModule, DatePipe, Spinner],
  templateUrl: './ai-assistant.html',
  styleUrl: './ai-assistant.css'
})
export class AiAssistant implements OnInit, AfterViewChecked {
  private readonly aiService = inject(AiService);
  private readonly authService = inject(AuthService);
  private readonly toastService = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  private readonly fb = inject(FormBuilder);

  @ViewChild('historyContainer')
  private historyContainer?: ElementRef<HTMLDivElement>;

  readonly assistantName = '🤖 OneNest AI';
  readonly displayName = this.authService.currentUser()?.fullName?.trim() || 'there';

  readonly isLoading = signal(false);
  readonly isSending = signal(false);
  readonly isConversationLoading = signal(false);

  readonly conversations = signal<ConversationSummary[]>([]);
  readonly selectedConversationId = signal<string | null>(null);
  readonly selectedConversation = signal<ConversationResponse | null>(null);

  readonly conversationSearch = signal('');
  readonly conversationFilter = signal<'all' | 'active' | 'archived'>('active');

  readonly suggestedPrompts = [
    'Summarize my day',
    'Motivate me',
    'Help me plan today',
    'Suggest healthy habits',
    'Explain JWT',
    'Generate study plan'
  ];

  readonly filteredConversations = computed(() => {
    const text = this.conversationSearch().trim().toLowerCase();
    const filter = this.conversationFilter();

    let list = this.conversations();

    if (filter === 'active') {
      list = list.filter(x => !x.isArchived);
    } else if (filter === 'archived') {
      list = list.filter(x => x.isArchived);
    }

    if (!text) return list;

    return list.filter(x =>
      (x.title ?? '').toLowerCase().includes(text)
    );
  });

  readonly messages = computed<ChatMessageResponse[]>(() =>
    this.selectedConversation()?.messages ?? []
  );

  readonly chatForm = this.fb.group({
    message: ['', [Validators.required, Validators.maxLength(8000)]]
  });

  readonly renameForm = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(120)]]
  });

  private shouldScroll = false;

  ngOnInit(): void {
    this.loadConversations(true);
  }

  ngAfterViewChecked(): void {
    if (this.shouldScroll) {
      this.scrollToBottom();
      this.shouldScroll = false;
    }
  }

  loadConversations(autoSelectNewest = false): void {
    this.isLoading.set(true);

    this.aiService.getConversations(true, this.conversationSearch())
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: items => {
          this.conversations.set(items);

          const filter = this.conversationFilter();
          const visibleItems = items.filter(x =>
            filter === 'all' || (filter === 'active' ? !x.isArchived : x.isArchived)
          );

          if (!visibleItems.length) {
            this.selectedConversationId.set(null);
            this.selectedConversation.set(null);
            return;
          }

          if (autoSelectNewest || !this.selectedConversationId()) {
            this.selectConversation(visibleItems[0].id);
            return;
          }

          const selected = this.selectedConversationId();
          const exists = visibleItems.some(x => x.id === selected);
          if (selected && exists) {
            this.selectConversation(selected, false);
          } else {
            this.selectConversation(visibleItems[0].id);
          }
        },
        error: () => this.toastService.error('Failed to load conversations')
      });
  }

  onConversationSearch(value: string): void {
    this.conversationSearch.set(value);
    this.loadConversations(false);
  }

  setConversationFilter(value: 'all' | 'active' | 'archived'): void {
    this.conversationFilter.set(value);

    const selected = this.selectedConversation();
    if (selected) {
      const filter = this.conversationFilter();
      if (filter === 'active' && selected.isArchived) {
        this.selectedConversationId.set(null);
        this.selectedConversation.set(null);
      } else if (filter === 'archived' && !selected.isArchived) {
        this.selectedConversationId.set(null);
        this.selectedConversation.set(null);
      }
    }
  }

  newChat(): void {
    this.isLoading.set(true);

    this.aiService.createConversation({})
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: conversation => {
          this.conversations.update(list => [
            {
              id: conversation.id,
              title: conversation.title || 'New Chat',
              lastMessageAt: conversation.lastMessageAt,
              createdAt: conversation.createdAt,
              isArchived: conversation.isArchived
            },
            ...list
          ]);

          this.selectedConversationId.set(conversation.id);
          this.selectedConversation.set(conversation);
          this.renameForm.patchValue({ title: conversation.title || '' });
          this.shouldScroll = true;
        },
        error: () => this.toastService.error('Failed to create new chat')
      });
  }

  selectConversation(conversationId: string, showLoader = true): void {
    this.selectedConversationId.set(conversationId);

    if (showLoader) {
      this.isConversationLoading.set(true);
    }

    this.aiService.getConversation(conversationId)
      .pipe(finalize(() => this.isConversationLoading.set(false)))
      .subscribe({
        next: conversation => {
          this.selectedConversation.set(conversation);
          this.renameForm.patchValue({ title: conversation.title || '' });
          this.shouldScroll = true;
        },
        error: () => this.toastService.error('Failed to load conversation')
      });
  }

  send(): void {
    if (this.chatForm.invalid || this.isSending()) {
      this.chatForm.markAllAsTouched();
      return;
    }

    const selected = this.selectedConversationId();
    if (!selected) {
      this.toastService.error('Create or select a conversation first');
      return;
    }

    const raw = this.chatForm.value.message ?? '';
    const message = raw.trim();
    if (!message) {
      this.toastService.error('Please enter a message');
      return;
    }

    const current = this.selectedConversation();
    if (!current) {
      this.toastService.error('Conversation is not loaded');
      return;
    }

    const optimisticMessage: ChatMessageResponse = {
      id: crypto.randomUUID(),
      role: 'user',
      content: message,
      timestamp: new Date().toISOString(),
      isError: false,
      usedWorkspaceData: false,
      responseMode: 'general',
      workspaceToolsUsed: []
    };

    this.selectedConversation.set({
      ...current,
      messages: [...current.messages, optimisticMessage]
    });

    this.chatForm.reset({ message: '' });
    this.shouldScroll = true;

    this.isSending.set(true);
    this.aiService.sendMessage(selected, { message })
      .pipe(finalize(() => this.isSending.set(false)))
      .subscribe({
        next: response => {
          const now = response.timestamp || new Date().toISOString();

          this.selectedConversation.update(conv => {
            if (!conv) return conv;

            return {
              ...conv,
              messages: [
                ...conv.messages,
                {
                  id: crypto.randomUUID(),
                  role: 'assistant',
                  content: response.response,
                  timestamp: now,
                  isError: false,
                  usedWorkspaceData: response.usedWorkspaceData,
                  responseMode: response.responseMode,
                  workspaceToolsUsed: response.workspaceToolsUsed ?? []
                }
              ],
              lastMessageAt: now
            };
          });

          this.loadConversations(false);
          this.shouldScroll = true;
        },
        error: err => {
          const status = Number(err?.status ?? 0);
          const serverMessage = typeof err?.error === 'string' ? err.error : '';

          if (status === 0) {
            this.toastService.error('Unable to reach AI service. Check your internet or backend connection.');
          } else if (status === 400 && serverMessage) {
            this.toastService.error(serverMessage);
          } else if (status === 401 || status === 403) {
            this.toastService.error('Unauthorized AI request. Please login again.');
          } else if (status === 429) {
            this.toastService.error('AI rate limit reached. Please wait and try again.');
          } else if (status === 404) {
            this.toastService.error('Conversation not found. Please select another conversation.');
          } else {
            this.toastService.error('AI is unavailable right now. Please try again shortly.');
          }

          this.selectConversation(selected, false);
        }
      });
  }

  onComposerKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  usePrompt(prompt: string): void {
    this.chatForm.patchValue({ message: prompt });
  }

  async copyMessage(content: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(content);
      this.toastService.success('Copied to clipboard');
    } catch {
      this.toastService.error('Unable to copy message');
    }
  }

  renameConversation(): void {
    const selected = this.selectedConversationId();
    if (!selected) {
      this.toastService.error('Select a conversation first');
      return;
    }

    if (this.renameForm.invalid) {
      this.renameForm.markAllAsTouched();
      return;
    }

    const title = (this.renameForm.value.title ?? '').trim();
    if (!title) {
      this.toastService.error('Title cannot be empty');
      return;
    }

    this.aiService.renameConversation(selected, { title })
      .subscribe({
        next: conversation => {
          this.selectedConversation.set(conversation);
          this.conversations.update(list => list.map(x =>
            x.id === selected
              ? { ...x, title: conversation.title, lastMessageAt: conversation.lastMessageAt }
              : x
          ));
          this.toastService.success('Conversation renamed');
        },
        error: () => this.toastService.error('Failed to rename conversation')
      });
  }

  archiveConversation(): void {
    const selected = this.selectedConversationId();
    if (!selected) {
      this.toastService.error('Select a conversation first');
      return;
    }

    this.aiService.archiveConversation(selected)
      .subscribe({
        next: () => {
          this.toastService.success('Conversation archived');
          this.loadConversations(true);
        },
        error: () => this.toastService.error('Failed to archive conversation')
      });
  }

  unarchiveConversation(): void {
    const selected = this.selectedConversationId();
    if (!selected) {
      this.toastService.error('Select a conversation first');
      return;
    }

    this.aiService.unarchiveConversation(selected)
      .subscribe({
        next: () => {
          this.toastService.success('Conversation restored');
          this.loadConversations(true);
        },
        error: () => this.toastService.error('Failed to restore conversation')
      });
  }

  async deleteConversation(): Promise<void> {
    const selected = this.selectedConversationId();
    if (!selected) {
      this.toastService.error('Select a conversation first');
      return;
    }

    const confirmed = await this.confirmService.confirm({
      title: 'Delete Conversation',
      message: 'Are you sure you want to delete this conversation? This action cannot be undone from the chat list.',
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });

    if (!confirmed) {
      return;
    }

    this.aiService.deleteConversation(selected)
      .subscribe({
        next: () => {
          this.toastService.success('Conversation deleted');
          this.loadConversations(true);
        },
        error: () => this.toastService.error('Failed to delete conversation')
      });
  }

  renderMessage(content: string): string {
    const escaped = this.escapeHtml(content ?? '');

    const withCodeBlock = escaped.replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>');
    const withInlineCode = withCodeBlock.replace(/`([^`]+)`/g, '<code>$1</code>');
    const withHeadings = withInlineCode
      .replace(/^###\s(.+)$/gm, '<h3>$1</h3>')
      .replace(/^##\s(.+)$/gm, '<h2>$1</h2>')
      .replace(/^#\s(.+)$/gm, '<h1>$1</h1>');
    const withBold = withHeadings.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    const withItalic = withBold.replace(/(^|\s)\*([^*]+)\*(?=\s|$)/g, '$1<em>$2</em>');
    const withLinks = withItalic.replace(
      /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g,
      '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>'
    );

    const withBullets = withLinks.replace(/(?:^|\n)-\s(.+)(?=\n|$)/g, '<li>$1</li>');
    const withLists = withBullets.replace(/(<li>.*<\/li>)/gs, '<ul>$1</ul>');

    return withLists.replace(/\n/g, '<br>');
  }

  private scrollToBottom(): void {
    const el = this.historyContainer?.nativeElement;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  }

  private escapeHtml(value: string): string {
    return value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }
}
