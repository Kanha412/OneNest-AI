import {
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { DatePipe } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

import { finalize } from 'rxjs';

import { DocumentsService } from '../../services/documents.service';
import {
  DocumentItem,
  DocumentCategory,
  DOCUMENT_CATEGORY_LABELS
} from '../../models/document.model';
import { Spinner } from '../../shared/spinner/spinner';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Paginator } from '../../shared/paginator/paginator';

type SortOption = 'latest' | 'oldest' | 'name' | 'size';

@Component({
  selector: 'app-documents',
  imports: [ReactiveFormsModule, DatePipe, Spinner, Paginator],
  templateUrl: './documents.html',
  styleUrl: './documents.css'
})
export class Documents implements OnInit {

  ngOnInit(): void {
    this.loadDocuments();
  }

  private readonly service = inject(DocumentsService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastService = inject(ToastService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly categoryLabels = DOCUMENT_CATEGORY_LABELS;

  readonly categories = [
    DocumentCategory.Resume,
    DocumentCategory.Certificate,
    DocumentCategory.Identity,
    DocumentCategory.Medical,
    DocumentCategory.Invoice,
    DocumentCategory.Finance,
    DocumentCategory.Education,
    DocumentCategory.Personal,
    DocumentCategory.Other
  ];

  readonly editingId = signal<string | null>(null);
  readonly search = signal('');
  readonly categoryFilter = signal<number | 'all'>('all');
  readonly sortBy = signal<SortOption>('latest');
  readonly currentPage = signal(1);
  readonly pageSize = 6;

  readonly documents = signal<DocumentItem[]>([]);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);

  readonly selectedFile = signal<File | null>(null);

  readonly previewUrl = signal<string | null>(null);
  readonly previewSafeUrl = signal<SafeResourceUrl | null>(null);
  readonly previewType = signal<string>('');
  readonly previewName = signal<string>('');
  readonly previewSupported = signal(false);
  readonly showPreview = signal(false);

  readonly documentForm = this.fb.group({
    title: ['', Validators.required],
    category: [DocumentCategory.Other],
    description: ['']
  });

  readonly filteredDocuments = computed(() => {
    const text = this.search().toLowerCase().trim();
    const category = this.categoryFilter();
    const sort = this.sortBy();

    let result = this.documents().filter(doc => {
      const matchesText =
        doc.title.toLowerCase().includes(text) ||
        doc.originalFileName.toLowerCase().includes(text) ||
        (doc.description ?? '').toLowerCase().includes(text);

      const matchesCategory =
        category === 'all' || doc.category === category;

      return matchesText && matchesCategory;
    });

    result = [...result].sort((a, b) => {
      switch (sort) {
        case 'oldest':
          return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
        case 'name':
          return a.title.localeCompare(b.title);
        case 'size':
          return b.fileSize - a.fileSize;
        case 'latest':
        default:
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
      }
    });

    return result;
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredDocuments().length / this.pageSize))
  );

  readonly pagedDocuments = computed(() => {
    const page = Math.min(this.currentPage(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredDocuments().slice(start, start + this.pageSize);
  });

  onSearch(value: string): void {
    this.search.set(value);
    this.currentPage.set(1);
  }

  onCategoryFilter(value: string): void {
    this.categoryFilter.set(value === 'all' ? 'all' : Number(value));
    this.currentPage.set(1);
  }

  onSort(value: string): void {
    this.sortBy.set(value as SortOption);
    this.currentPage.set(1);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile.set(file);

    if (file && !this.documentForm.value.title) {
      const nameWithoutExt = file.name.replace(/\.[^/.]+$/, '');
      this.documentForm.patchValue({ title: nameWithoutExt });
    }
  }

  loadDocuments(): void {
    this.isLoading.set(true);

    this.service
      .getDocuments()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: docs => this.documents.set(docs),
        error: () => this.toastService.error('Failed to load documents')
      });
  }

  saveDocument(): void {
    if (this.editingId()) {
      this.updateMetadata();
      return;
    }

    if (this.documentForm.invalid) {
      this.documentForm.markAllAsTouched();
      return;
    }

    const file = this.selectedFile();

    if (!file) {
      this.toastService.error('Please choose a file to upload');
      return;
    }

    const maxBytes = 25 * 1024 * 1024;
    if (file.size > maxBytes) {
      this.toastService.error('File size exceeds the 25 MB limit');
      return;
    }

    this.isSaving.set(true);

    this.service
      .upload(
        file,
        this.documentForm.value.title!,
        Number(this.documentForm.value.category),
        this.documentForm.value.description ?? ''
      )
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: () => {
          this.resetForm();
          this.loadDocuments();
          this.toastService.success('Document uploaded');
        },
        error: err => {
          const serverMessage = typeof err?.error === 'string' ? err.error : '';
          if (serverMessage) {
            this.toastService.error(serverMessage);
            return;
          }

          this.toastService.error('Upload failed. Ensure total storage stays within 150 MB.');
        }
      });
  }

  private updateMetadata(): void {
    if (this.documentForm.invalid) {
      this.documentForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);

    this.service
      .updateDocument(this.editingId()!, {
        title: this.documentForm.value.title!,
        category: Number(this.documentForm.value.category),
        description: this.documentForm.value.description ?? ''
      })
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: () => {
          this.resetForm();
          this.loadDocuments();
          this.toastService.success('Document updated');
        },
        error: () => this.toastService.error('Failed to update document')
      });
  }

  editDocument(doc: DocumentItem): void {
    this.editingId.set(doc.id);

    this.documentForm.patchValue({
      title: doc.title,
      category: doc.category,
      description: doc.description
    });
  }

  cancelEdit(): void {
    this.resetForm();
  }

  deleteDocument(id: string): void {
    this.confirmService.confirm({
      title: 'Delete document',
      message: 'Are you sure you want to delete this document?',
      confirmText: 'Delete'
    }).then(confirmed => {
      if (!confirmed) {
        return;
      }

      this.service.deleteDocument(id)
        .subscribe({
          next: () => {
            this.loadDocuments();
            this.toastService.success('Document deleted');
          },
          error: () => this.toastService.error('Failed to delete document')
        });
    });
  }

  download(doc: DocumentItem): void {
    this.service.downloadFile(doc.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = doc.originalFileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastService.error('Failed to download document')
    });
  }

  preview(doc: DocumentItem): void {
    const type = doc.contentType?.toLowerCase() ?? '';
    const supported =
      type === 'application/pdf' ||
      type.startsWith('image/') ||
      type.startsWith('text/');

    if (!supported) {
      this.download(doc);
      return;
    }

    this.service.previewFile(doc.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        this.previewUrl.set(url);
        this.previewSafeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
        this.previewType.set(type);
        this.previewName.set(doc.originalFileName);
        this.previewSupported.set(true);
        this.showPreview.set(true);
      },
      error: () => this.toastService.error('Failed to preview document')
    });
  }

  closePreview(): void {
    const url = this.previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
    }
    this.previewUrl.set(null);
    this.previewSafeUrl.set(null);
    this.showPreview.set(false);
  }

  isImagePreview(): boolean {
    return this.previewType().startsWith('image/');
  }

  isPdfPreview(): boolean {
    return this.previewType() === 'application/pdf';
  }

  isTextPreview(): boolean {
    return this.previewType().startsWith('text/');
  }

  fileIcon(doc: DocumentItem): string {
    const type = doc.contentType?.toLowerCase() ?? '';
    const ext = doc.originalFileName?.split('.').pop()?.toLowerCase() ?? '';

    if (type === 'application/pdf' || ext === 'pdf') return '📄';
    if (type.includes('word') || ext === 'doc' || ext === 'docx') return '📝';
    if (type.includes('sheet') || type.includes('excel') || ['xls', 'xlsx', 'csv'].includes(ext)) return '📊';
    if (type.includes('presentation') || type.includes('powerpoint') || ['ppt', 'pptx'].includes(ext)) return '📽️';
    if (type.startsWith('image/') || ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'svg'].includes(ext)) return '🖼️';
    if (['zip', 'rar', '7z', 'tar', 'gz'].includes(ext)) return '🗜️';
    if (type.startsWith('text/') || ['txt', 'rtf'].includes(ext)) return '📃';
    return '📑';
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  private resetForm(): void {
    this.editingId.set(null);
    this.selectedFile.set(null);
    this.documentForm.reset({
      title: '',
      category: DocumentCategory.Other,
      description: ''
    });
  }
}
