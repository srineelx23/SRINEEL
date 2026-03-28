import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

declare var Razorpay: any;

interface RazorpayOrderPayload {
  keyId: string;
  orderId: string;
  policyNumber?: string;
  currency?: string;
  email?: string;
  contact?: string;
}

export interface RazorpayVerifyPayload {
  policyId: number;
  razorpayOrderId: string;
  razorpayPaymentId: string;
  razorpaySignature: string;
}

export interface CreateOrderResponse {
  orderId: string;
  keyId: string;
  amount: number;
  currency: string;
  policyNumber: string;
  baseAmount: number;
  discountAmount: number;
  finalAmount: number;
}

@Injectable({
  providedIn: 'root'
})
export class RazorpayService {
  private http = inject(HttpClient);
  // Replace with your endpoint prefix if needed
  private readonly baseUrl = 'https://localhost:7257/api/payment';
  private readonly checkoutScriptId = 'razorpay-checkout-sdk';

  private ensureCheckoutScriptLoaded(): Promise<void> {
    return new Promise((resolve, reject) => {
      const existingScript = document.getElementById(this.checkoutScriptId) as HTMLScriptElement | null;

      if (existingScript) {
        if ((window as any).Razorpay) {
          resolve();
          return;
        }

        existingScript.addEventListener('load', () => resolve(), { once: true });
        existingScript.addEventListener('error', () => reject(new Error('Failed to load Razorpay checkout SDK.')), {
          once: true
        });
        return;
      }

      const script = document.createElement('script');
      script.id = this.checkoutScriptId;
      script.src = 'https://checkout.razorpay.com/v1/checkout.js';
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Failed to load Razorpay checkout SDK.'));
      document.body.appendChild(script);
    });
  }

  async launchCheckout(
    orderData: RazorpayOrderPayload,
    onSuccess: (response: any) => void,
    onFailure?: (errorMessage: string) => void
  ): Promise<void> {
    if (!orderData || !orderData.keyId?.trim() || !orderData.orderId?.trim()) {
      onFailure?.('Payment gateway returned an invalid order. Please retry.');
      return;
    }

    await this.ensureCheckoutScriptLoaded();

    const options = {
      key: orderData.keyId,
      name: "VIMS Insurance",
      description: `Payment for Order ${orderData.policyNumber}`,
      order_id: orderData.orderId,
      currency: orderData.currency || 'INR',
      prefill: {
        email: orderData.email || "",
        contact: orderData.contact || "9999999999"
      },
      handler: (response: any) => {
        onSuccess(response);
      }
    };
    const rzp = new Razorpay(options);
    rzp.on('payment.failed', (response: any) => {
      const error = response?.error;
      const message = [
        error?.description,
        error?.reason,
        error?.source ? `Source: ${error.source}` : '',
        error?.step ? `Step: ${error.step}` : '',
        error?.code ? `Code: ${error.code}` : ''
      ]
        .filter(Boolean)
        .join(' | ') || 'Payment failed. Please try again.';
      onFailure?.(message);
    });
    rzp.open();
  }

  createOrder(policyId: number): Observable<CreateOrderResponse> {
    return this.http.post<CreateOrderResponse>(`${this.baseUrl}/create-order/${policyId}`, {});
  }

  verifyPayment(verificationData: RazorpayVerifyPayload) {
    return this.http.post<any>(`${this.baseUrl}/verify`, verificationData);
  }
}
