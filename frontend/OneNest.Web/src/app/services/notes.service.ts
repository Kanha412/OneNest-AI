import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Note } from '../models/note.model';

@Injectable({
  providedIn: 'root'
})
export class NotesService {

  private http = inject(HttpClient);

  private readonly apiUrl = 'http://localhost:5189/api/Notes';

  getNotes() {
   return this.http.get<Note[]>(this.apiUrl);
}

  createNote(note: Partial<Note>): Observable<Note> {
    return this.http.post<Note>(this.apiUrl, note);
  }

  deleteNote(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  updateNote(id: string, note: Partial<Note>) {

    return this.http.put<Note>(`${this.apiUrl}/${id}`, note);

}

togglePin(id: string) {
  return this.http.patch(`${this.apiUrl}/${id}/pin`, {});
}
}