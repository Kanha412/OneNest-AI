export enum DocumentCategory {
  Resume = 1,
  Certificate = 2,
  Identity = 3,
  Medical = 4,
  Invoice = 5,
  Finance = 6,
  Education = 7,
  Personal = 8,
  Other = 9
}

export const DOCUMENT_CATEGORY_LABELS: Record<number, string> = {
  [DocumentCategory.Resume]: 'Resume',
  [DocumentCategory.Certificate]: 'Certificate',
  [DocumentCategory.Identity]: 'Identity',
  [DocumentCategory.Medical]: 'Medical',
  [DocumentCategory.Invoice]: 'Invoice',
  [DocumentCategory.Finance]: 'Finance',
  [DocumentCategory.Education]: 'Education',
  [DocumentCategory.Personal]: 'Personal',
  [DocumentCategory.Other]: 'Other'
};

export interface DocumentItem {
  id: string;
  title: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  category: DocumentCategory;
  description: string;
  // Phase 6 — AI Document Intelligence
  isTextExtracted: boolean;
  aiSummary: string | null;
  aiSummarizedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CategoryDistribution {
  category: DocumentCategory;
  count: number;
}

export interface DocumentSummary {
  totalDocuments: number;
  todayUploads: number;
  storageUsed: number;
  recentDocuments: DocumentItem[];
  categoryDistribution: CategoryDistribution[];
}
