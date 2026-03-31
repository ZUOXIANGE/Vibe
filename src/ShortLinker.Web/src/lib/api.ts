import axios from 'axios';
import { getTenantId } from './tenant';

export interface ApiResponse<T> {
    code: number;
    msg: string;
    data: T;
}

export const api = axios.create({
    baseURL: '/api/v1',
    timeout: 10000,
});

api.interceptors.request.use((config) => {
    config.headers['X-Tenant-Id'] = getTenantId();
    return config;
});

api.interceptors.response.use(
    (response) => {
        const res = response.data as ApiResponse<any>;
        if (res.code !== 200) {
            return Promise.reject(new Error(res.msg || '业务异常'));
        }
        return res.data;
    },
    (error) => {
        return Promise.reject(error);
    }
);
