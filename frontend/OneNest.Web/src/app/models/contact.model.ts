export enum ContactCategory {
  General = 0,
  Support = 1,
  Bug = 2,
  Feedback = 3
}

export enum ContactStatus {
  New = 0,
  Read = 1,
  Resolved = 2
}

export const CONTACT_CATEGORY_LABELS: Record<ContactCategory, string> = {
  [ContactCategory.General]: 'General',
  [ContactCategory.Support]: 'Support',
  [ContactCategory.Bug]: 'Bug Report',
  [ContactCategory.Feedback]: 'Feedback'
};

export const CONTACT_STATUS_LABELS: Record<ContactStatus, string> = {
  [ContactStatus.New]: 'New',
  [ContactStatus.Read]: 'Read',
  [ContactStatus.Resolved]: 'Resolved'
};

export interface ContactMessage {
  id: string;
  userId: string;
  userName: string;
  userEmail: string;
  subject: string;
  message: string;
  category: ContactCategory;
  status: ContactStatus;
  adminReply?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateContactRequest {
  subject: string;
  message: string;
  category: ContactCategory;
}

export interface UpdateContactStatusRequest {
  status: ContactStatus;
  adminReply?: string;
}

export interface ContactSummary {
  totalMessages: number;
  newCount: number;
  readCount: number;
  resolvedCount: number;
  recentMessages: ContactMessage[];
}
