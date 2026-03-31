export const getTenantId = (): string => {
    // 优先从 localStorage 获取（用于开发/调试）
    const stored = localStorage.getItem('tenant_id');
    if (stored) return stored;

    // 从子域名获取
    const host = window.location.hostname;
    const parts = host.split('.');
    if (parts.length >= 3 && parts[0] !== 'www') {
        return parts[0];
    }
    
    // 默认回退
    return 'default';
};
