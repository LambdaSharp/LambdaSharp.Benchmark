# AWS Lambda Performance for C#, Python, and NodeJS

Various implementations of a simple Lambda function to test performance.

This is the outcome of a quick and dirty performance test to compare the performance of various AWS lambda engines.
All tests were done by sending the string `"foo"` as input to each respective function using the AWS console. 
The test setup extremely simple: repeatedly keep clicking the `Test` button in the AWS Lambda console and select 
a representative log statement.

The functions were deployed in `us-east-1` with their respective default settings. 
The C# function was deployed using `dotnet lambda deploy-function`. 
The Node and Python functions were deployed using the `HelloWorld` sample code for each respective language and 
changing the implementation so a simple string would be accepted and converted to uppercase.

**Python**
```
REPORT Duration: 0.20 ms    Billed Duration: 100 ms     Memory Size: 128 MB    Max Memory Used: 15 MB
```

**Javascript/NodeJS**
```
REPORT Duration: 0.27 ms    Billed Duration: 100 ms     Memory Size: 128 MB    Max Memory Used: 16 MB
```

**C#**
```
REPORT Duration: 0.85 ms    Billed Duration: 100 ms     Memory Size: 256 MB    Max Memory Used: 42 MB
```
