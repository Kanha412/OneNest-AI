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
