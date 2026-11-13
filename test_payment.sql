UPDATE pickup_requests SET status = 'AWAITING_PAYMENT', updated_at = NOW() WHERE status = 'SELLER_CONFIRMED';
