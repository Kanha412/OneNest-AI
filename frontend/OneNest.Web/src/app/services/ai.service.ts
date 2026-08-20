import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ChatResponse,
  ConversationResponse,
  ConversationSummary,
  CreateConversationRequest,
  RagRequest,
  RagResponse,
  RenameConversationRequest,
  SendMessageRequest
} from '../models/ai.model';

@Injectable({
  providedIn: 'root'
})
export class AiService {

  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/ai`;

  getConversations(includeArchived = false, search?: string): Observable<ConversationSummary[]> {
    const params: Record<string, string> = {
      includeArchived: String(includeArchived)
    };

    if (search && search.trim()) {
      params['search'] = search.trim();
    }

    return this.http.get<ConversationSummary[]>(`${this.baseUrl}/conversations`, { params });
  }

  getConversation(id: string): Observable<ConversationResponse> {
    return this.http.get<ConversationResponse>(`${this.baseUrl}/conversations/${id}`);
  }

  createConversation(payload: CreateConversationRequest = {}): Observable<ConversationResponse> {
    return this.http.post<ConversationResponse>(`${this.baseUrl}/conversations`, payload);
  }

  sendMessage(conversationId: string, payload: SendMessageRequest): Observable<ChatResponse> {
    return this.http.post<ChatResponse>(`${this.baseUrl}/conversations/${conversationId}/messages`, payload);
  }

  renameConversation(conversationId: string, payload: RenameConversationRequest): Observable<ConversationResponse> {
    return this.http.put<ConversationResponse>(`${this.baseUrl}/conversations/${conversationId}/rename`, payload);
  }

  deleteConversation(conversationId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/conversations/${conversationId}`);
  }

  archiveConversation(conversationId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/conversations/${conversationId}/archive`, {});
  }

  unarchiveConversation(conversationId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/conversations/${conversationId}/unarchive`, {});
  }

  /** Phase 9 — RAG: answer a query grounded in the user's personal content. */
  askRag(request: RagRequest): Observable<RagResponse> {
    return this.http.post<RagResponse>(`${this.baseUrl}/rag`, request);
  }
}
