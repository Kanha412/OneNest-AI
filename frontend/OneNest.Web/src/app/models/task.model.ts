export interface TaskItem {

  id: string;

  title: string;

  description: string;

  dueDate: string | null;

  priority: number;

  isCompleted: boolean;

  createdAt: string;

  updatedAt: string | null;

  completedAt: string | null;
}