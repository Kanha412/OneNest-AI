import { Component, inject } from '@angular/core';
import { NotesService } from '../../services/notes.service';
import { Note } from '../../models/note.model';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-notes',
  imports: [],
  templateUrl: './notes.html',
  styleUrl: './notes.css'
})
export class Notes {

  private notesService = inject(NotesService);

  readonly notes = toSignal(this.notesService.getNotes(), {
    initialValue: []
  });

}