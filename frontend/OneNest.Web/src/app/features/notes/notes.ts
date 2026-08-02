import { Component, computed, inject, signal } from '@angular/core';
import { NotesService } from '../../services/notes.service';
import { Note } from '../../models/note.model';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-notes',
  imports: [ReactiveFormsModule],
  templateUrl: './notes.html',
  styleUrl: './notes.css'
})
export class Notes {

  private readonly notesService = inject(NotesService);
  private readonly fb = inject(FormBuilder);

  readonly noteForm = this.fb.group({
  title: ['', Validators.required],
  content: ['', Validators.required]
});

  readonly notes = toSignal(this.notesService.getNotes(), {
    initialValue: []
  });

  readonly searchText = signal('');
  editingId = signal<string | null>(null);

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

    this.notesService
      .updateNote(this.editingId()!, noteData)
      .subscribe(() => {

        this.editingId.set(null);
        this.noteForm.reset();

        location.reload();

      });

  } else {

    this.notesService
      .createNote(noteData)
      .subscribe(() => {

        this.noteForm.reset();

        location.reload();

      });

  }

}
deleteNote(id: string) {

  if (!confirm('Delete this note?')) {
    return;
  }

  this.notesService.deleteNote(id)
    .subscribe(() => {

      location.reload();

    });

}

editNote(note: Note) {

  this.editingId.set(note.id);

  this.noteForm.patchValue({
    title: note.title,
    content: note.content
  });

}

togglePin(id: string) {

  this.notesService
      .togglePin(id)
      .subscribe(() => {

          location.reload();

      });

}
}