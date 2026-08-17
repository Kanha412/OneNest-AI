import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NotesService } from '../../services/notes.service';
import { Note } from '../../models/note.model';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { Spinner } from '../../shared/spinner/spinner';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Paginator } from '../../shared/paginator/paginator';

@Component({
  selector: 'app-notes',
  imports: [ReactiveFormsModule, Spinner, Paginator],
  templateUrl: './notes.html',
  styleUrl: './notes.css'
})
export class Notes implements OnInit {

  private readonly notesService = inject(NotesService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastService = inject(ToastService);

  readonly noteForm = this.fb.group({
  title: ['', Validators.required],
  content: ['', Validators.required]
});

  readonly notes = signal<Note[]>([]);

  readonly searchText = signal('');
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly currentPage = signal(1);
  readonly pageSize = 5;
  editingId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadNotes();
  }

  private loadNotes(): void {
    this.isLoading.set(true);
    this.notesService.getNotes()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: notes => this.notes.set(notes),
        error: () => this.toastService.error('Failed to load notes')
      });
  }

readonly filteredNotes = computed(() => {

  const search = this.searchText().toLowerCase().trim();

  const filtered = this.notes().filter(note =>
    note.title.toLowerCase().includes(search) ||
    note.content.toLowerCase().includes(search)
  );

  return filtered.sort((a, b) =>
    Number(b.isPinned) - Number(a.isPinned)
  );

});

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredNotes().length / this.pageSize))
  );

  readonly pagedNotes = computed(() => {
    const page = Math.min(this.currentPage(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredNotes().slice(start, start + this.pageSize);
  });

  onSearch(value: string): void {
    this.searchText.set(value);
    this.currentPage.set(1);
  }

  saveNote(): void {

  if (this.noteForm.invalid) {
    this.noteForm.markAllAsTouched();
    return;
  }

  const noteData = {
    title: this.noteForm.value.title!,
    content: this.noteForm.value.content!
  };

  if (this.editingId()) {

    this.isSaving.set(true);
    this.notesService
      .updateNote(this.editingId()!, noteData)
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: () => {

          this.editingId.set(null);
          this.noteForm.reset();

          this.loadNotes();

          this.toastService.success('Note updated');

        },
        error: () => this.toastService.error('Failed to update note')
      });

  } else {

    this.isSaving.set(true);
    this.notesService
      .createNote(noteData)
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: () => {

          this.noteForm.reset();

          this.loadNotes();

          this.toastService.success('Note created');

        },
        error: () => this.toastService.error('Failed to create note')
      });

  }

}
deleteNote(id: string) {

  this.confirmService.confirm({
    title: 'Delete note',
    message: 'Are you sure you want to delete this note?',
    confirmText: 'Delete'
  }).then(confirmed => {

    if (!confirmed) {
      return;
    }

    this.notesService.deleteNote(id)
      .subscribe({
        next: () => {

          this.loadNotes();

          this.toastService.success('Note deleted');

        },
        error: () => this.toastService.error('Failed to delete note')
      });

  });

}

editNote(note: Note) {

  this.editingId.set(note.id);

  this.noteForm.patchValue({
    title: note.title,
    content: note.content
  });

}

cancelEdit() {
  this.editingId.set(null);
  this.noteForm.reset();
}

togglePin(id: string) {

  const note = this.notes().find(n => n.id === id);
  const wasPinned = note?.isPinned;

  this.notesService
      .togglePin(id)
      .subscribe({
        next: () => {
          this.loadNotes();
          this.toastService.success(wasPinned ? 'Note unpinned' : 'Note pinned');
        },
        error: () => this.toastService.error('Failed to update note')
      });

}
}
