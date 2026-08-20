export interface ChatRequest {
  message: string;
}

export interface ChatResponse {
  response: string;
  model: string;
  timestamp: string;
  usedWorkspaceData: boolean;
  responseMode: 'general' | 'workspace';
  workspaceToolsUsed: string[];
}

export interface ConversationMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
}

export interface CreateConversationRequest {
  title?: string;
}

export interface RenameConversationRequest {
  title: string;
}

export interface SendMessageRequest {
  message: string;
}

export interface ChatMessageResponse {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  isError: boolean;
  usedWorkspaceData: boolean;
  responseMode: 'general' | 'workspace';
  workspaceToolsUsed: string[];
}

export interface ConversationSummary {
  id: string;
  title: string;
  lastMessageAt: string;
  createdAt: string;
  isArchived: boolean;
}

export interface ConversationResponse {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string | null;
  lastMessageAt: string;
  isArchived: boolean;
  messages: ChatMessageResponse[];
}

// ── Phase 9 — RAG models ──────────────────────────────────────────────────────

export interface RagSource {
  /** "Note" or "Document" */
  sourceType: string;
  title: string;
  chunkIndex: number;
}

export interface RagRequest {
  query: string;
  topK?: number;
  similarityThreshold?: number;
  conversationMessages?: ConversationMessage[];
}

export interface RagResponse {
  answer: string;
  sources: RagSource[];
  hasSources: boolean;
  model: string;
  timestamp: string;
}
