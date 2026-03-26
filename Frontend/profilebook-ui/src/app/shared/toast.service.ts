import { Injectable } from '@angular/core';

type ToastType = 'success' | 'error' | 'info';

@Injectable({ providedIn: 'root' })
export class ToastService {
  show(message: string, type: ToastType = 'info', timeout = 4500) {
    try {
      const containerId = 'app-toast-container';
      let container = document.getElementById(containerId);
      if (!container) {
        container = document.createElement('div');
        container.id = containerId;
        container.style.position = 'fixed';
        container.style.top = '20px';
        container.style.right = '20px';
        container.style.zIndex = '99999';
        container.style.display = 'flex';
        container.style.flexDirection = 'column';
        container.style.gap = '8px';
        document.body.appendChild(container);
      }

      const toast = document.createElement('div');
      toast.textContent = message;
      toast.style.minWidth = '220px';
      toast.style.padding = '10px 14px';
      toast.style.borderRadius = '8px';
      toast.style.boxShadow = '0 4px 12px rgba(0,0,0,0.12)';
      toast.style.color = '#111827';
      toast.style.fontSize = '14px';
      toast.style.opacity = '0';
      toast.style.transition = 'opacity 180ms ease, transform 180ms ease';
      toast.style.transform = 'translateY(-6px)';

      if (type === 'success') {
        toast.style.background = '#dcfce7';
        toast.style.border = '1px solid #86efac';
      } else if (type === 'error') {
        toast.style.background = '#fee2e2';
        toast.style.border = '1px solid #fca5a5';
      } else {
        toast.style.background = '#e0f2fe';
        toast.style.border = '1px solid #7dd3fc';
      }

      container.appendChild(toast);

      // animate in
      requestAnimationFrame(() => {
        toast.style.opacity = '1';
        toast.style.transform = 'translateY(0)';
      });

      const tid = setTimeout(() => {
        // animate out
        toast.style.opacity = '0';
        toast.style.transform = 'translateY(-6px)';
        setTimeout(() => {
          toast.remove();
        }, 200);
        clearTimeout(tid);
      }, timeout);
    } catch (e) {
      // fallback
      alert(message);
    }
  }

  success(message: string, timeout = 4000) {
    this.show(message, 'success', timeout);
  }

  error(message: string, timeout = 6000) {
    this.show(message, 'error', timeout);
  }

  info(message: string, timeout = 4000) {
    this.show(message, 'info', timeout);
  }
}
