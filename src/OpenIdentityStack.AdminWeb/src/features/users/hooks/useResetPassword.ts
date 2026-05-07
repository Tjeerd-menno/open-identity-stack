/**
 * useResetPassword Hook
 * 
 * React Query mutation hook for resetting a user's password
 */

import { useMutation } from '@tanstack/react-query';
import { resetPassword } from '../api/users-api';
import type { ResetPasswordRequest, PasswordResetResponse } from '@/types';

interface ResetPasswordVariables {
  userId: string;
  data: ResetPasswordRequest;
}

export function useResetPassword() {
  return useMutation<PasswordResetResponse, Error, ResetPasswordVariables>({
    mutationFn: ({ userId, data }) => resetPassword(userId, data),
  });
}
