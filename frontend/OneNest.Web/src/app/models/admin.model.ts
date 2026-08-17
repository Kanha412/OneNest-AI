export interface AdminUser {
  id: string;
  fullName: string;
  email: string;
  role: string;
  createdAt: string;
  lastLoginAt?: string;
}

export interface UpdateUserRoleRequest {
  role: string;
}
