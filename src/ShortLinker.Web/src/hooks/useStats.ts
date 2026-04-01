import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';

export interface StatsSummary {
    totalLinks: number;
    totalClicks: number;
}

export interface StatsTrend {
    date: string;
    count: number;
}

export interface StatsDevice {
    name: string;
    value: number;
}

export const useStatsSummary = () => {
    return useQuery<StatsSummary>({
        queryKey: ['stats', 'summary'],
        queryFn: () => api.get('/stats/summary') as Promise<StatsSummary>
    });
};

export const useStatsTrend = (days: number = 7) => {
    return useQuery<StatsTrend[]>({
        queryKey: ['stats', 'trend', days],
        queryFn: () => api.get(`/stats/clicks?days=${days}`) as Promise<StatsTrend[]>
    });
};

export const useStatsDevices = () => {
    return useQuery<StatsDevice[]>({
        queryKey: ['stats', 'devices'],
        queryFn: () => api.get('/stats/devices') as Promise<StatsDevice[]>
    });
};
