import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import Screen from '../../components/common/Screen';
import Button from '../../components/common/Button';
import { useNavigation } from '@react-navigation/native';
import { styles as globalStyles } from '../../theme';

export default function SellerHomeScreen() {
  const navigation = useNavigation();

  return (
    <Screen>
      <View style={styles.container}>
        <Text style={globalStyles.title}>Chào mừng bạn!</Text>
        <Text style={globalStyles.text}>
          Bán phế liệu thật dễ dàng. Bấm vào nút bên dưới để tạo yêu cầu thu gom hoặc gọi cho các kho vựa xung quanh bạn.
        </Text>
        
        <View style={styles.buttonContainer}>
          <Button
            title="Nổ đơn / Tìm kho ngay"
            onPress={() => navigation.navigate('CreateRequest')}
          />
        </View>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 16,
    justifyContent: 'center',
    alignItems: 'center',
  },
  buttonContainer: {
    marginTop: 32,
    width: '100%',
  }
});
