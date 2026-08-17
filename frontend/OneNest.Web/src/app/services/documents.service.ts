import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DocumentItem,
  DocumentSummary,
  DocumentCategory
} from '../models/document.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class DocumentsService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiBaseUrl}/Documents`;

  getDocuments(search?: string, category?: DocumentCategory | null): Observable<DocumentItem[]> {
    const params: Record<string, string> = {};

    if (search && search.trim()) {
      params['search'] = search.trim();
    }

    if (category !== null && category !== undefined) {
      params['category'] = String(category);
    }

    return this.http.get<DocumentItem[]>(this.apiUrl, { params });
  }

  getSummary(): Observable<DocumentSummary> {
    return this.http.get<DocumentSummary>(`${this.apiUrl}/summary`);
  }

  getRecent(count = 5): Observable<DocumentItem[]> {
    return this.http.get<DocumentItem[]>(`${this.apiUrl}/recent`, {
      params: { count: String(count) }
    });
  }

  upload(
    file: File,
    title: string,
    category: DocumentCategory,
    description: string
  ): Observable<DocumentItem> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('title', title);
    formData.append('category', String(category));
    formData.append('description', description ?? '');

    return this.http.post<DocumentItem>(this.apiUrl, formData);
  }

  updateDocument(
    id: string,
    payload: { title: string; category: DocumentCategory; description: string }
  ): Observable<DocumentItem> {
    return this.http.put<DocumentItem>(`${this.apiUrl}/${id}`, payload);
  }

  deleteDocument(id: string) {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  deleteAllDocuments(): Observable<{ deletedCount: number }> {
    return this.http.delete<{ deletedCount: number }>(`${this.apiUrl}/all`);
  }

  downloadFile(id: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/download`, {
      responseType: 'blob'
    });
  }

  downloadAllFiles(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/download-all`, {
      responseType: 'blob'
    });
  }

  previewFile(id: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/preview`, {
      responseType: 'blob'
    });
  }

  // Phase 6 — AI Document Intelligence

  getExtractedText(id: string): Observable<{ text: string }> {
    return this.http.get<{ text: string }>(`${this.apiUrl}/${id}/text`);
  }

  summarize(id: string): Observable<DocumentItem> {
    return this.http.post<DocumentItem>(`${this.apiUrl}/${id}/summarize`, {});
  }
}
