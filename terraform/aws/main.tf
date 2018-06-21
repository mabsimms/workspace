provider "aws" { 
    region = "us-east-1"
}

resource "aws_instance" "example" {
  ami = "ami-2d39803a"
  instance_type = "t2.micro"
}
