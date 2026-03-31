import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api';
import type { ShortLink } from '../types';

export const useLinks = () => {
    return useQuery<ShortLink[]>({
        queryKey: ['links'],
        queryFn: () => api.get('/links')
    });
};

export const useCreateLink = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (originalUrl: string) => api.post('/links', `"${originalUrl}"`, {
            headers: { 'Content-Type': 'application/json' }
        }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['links'] });
        }
    });
};
